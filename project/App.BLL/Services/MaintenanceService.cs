using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using Shared.Contracts.Charging;
using DomainMaintenanceStatus = App.Domain.EMaintenanceStatus;
using DomainMaintenance = App.Domain.Maintenance;

namespace App.BLL.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly IUnitOfWork _unitOfWork;

    public MaintenanceService(IChargingModuleApi chargingModuleApi, IUnitOfWork unitOfWork)
    {
        _chargingModuleApi = chargingModuleApi;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<List<MaintenanceIssueDto>>> GetIssuesAsync(Guid companyId, bool includeResolved = true)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<MaintenanceIssueDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var issues = await _chargingModuleApi.GetMaintenancesByCompanyAsync(companyId, includeResolved);
        return ServiceResult<List<MaintenanceIssueDto>>.Ok(issues.Select(MapIssue).ToList());
    }

    public async Task<ServiceResult<MaintenanceIssueDto>> GetByIdAsync(Guid id, Guid companyId)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("VALIDATION", "Issue id and company id are required.");
        }

        var issue = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, companyId);
        if (issue == null)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("FORBIDDEN", "Maintenance issue not found or access denied.");
        }

        return ServiceResult<MaintenanceIssueDto>.Ok(MapIssue(issue));
    }

    public async Task<ServiceResult<MaintenanceIssueDto>> CreateMaintenanceAsync(Guid stationId, Guid companyId, Guid userId, string issueDescription)
    {
        if (stationId == Guid.Empty || companyId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("VALIDATION", "Station, company and user are required.");
        }

        if (string.IsNullOrWhiteSpace(issueDescription))
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("VALIDATION", "Issue description is required.");
        }

        var station = await _chargingModuleApi.GetStationByIdAsync(stationId);
        if (station == null || station.CompanyId != companyId)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var issue = new MaintenanceContract
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ChargingStationId = stationId,
            StationName = station.Name,
            ReportedByUserId = userId,
            IssueDescription = issueDescription.Trim(),
            Status = Shared.Contracts.Charging.EMaintenanceStatus.Reported,
            ReportedAtUtc = DateTime.UtcNow
        };

        var created = await _chargingModuleApi.CreateMaintenanceAsync(issue);
        return ServiceResult<MaintenanceIssueDto>.Ok(MapIssue(created));
    }

    public async Task<ServiceResult<MaintenanceIssueDto>> UpdateStatusAsync(
        Guid id,
        Guid companyId,
        Guid userId,
        DomainMaintenanceStatus newStatus,
        string? notes)
    {
        if (id == Guid.Empty || companyId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("VALIDATION", "Issue id, company id and user id are required.");
        }

        var issue = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, companyId);
        if (issue == null)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("FORBIDDEN", "Maintenance issue not found or access denied.");
        }

        if (!IsValidTransition(MapMaintenanceStatus(issue.Status), newStatus))
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("VALIDATION", "Invalid maintenance status transition.");
        }

        var resolvedAtUtc = newStatus == DomainMaintenanceStatus.Resolved ? DateTime.UtcNow : (DateTime?)null;

        var maintenanceUpdated = await _chargingModuleApi.UpdateMaintenanceStatusAsync(
            id,
            MapMaintenanceStatus(newStatus),
            string.IsNullOrWhiteSpace(notes) ? issue.Notes : notes.Trim(),
            resolvedAtUtc);

        if (!maintenanceUpdated)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("NOT_FOUND", "Maintenance issue was not found.");
        }

        var targetStationStatus = newStatus == DomainMaintenanceStatus.Resolved
            ? Shared.Contracts.Charging.EStationStatus.Available
            : Shared.Contracts.Charging.EStationStatus.Maintenance;

        var stationUpdated = await _chargingModuleApi.UpdateStationStatusAsync(issue.ChargingStationId, targetStationStatus);
        if (!stationUpdated)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var updated = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, companyId);
        return updated == null
            ? ServiceResult<MaintenanceIssueDto>.Fail("NOT_FOUND", "Maintenance issue could not be loaded after update.")
            : ServiceResult<MaintenanceIssueDto>.Ok(MapIssue(updated));
    }

    public async Task<ServiceResult<MaintenanceIssueDto>> AssignAsync(Guid id, Guid companyId, Guid userId, Guid? assignedToUserId)
    {
        if (id == Guid.Empty || companyId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("VALIDATION", "Issue id, company id and user id are required.");
        }

        var issue = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, companyId);
        if (issue == null)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("FORBIDDEN", "Maintenance issue not found or access denied.");
        }

        var assigned = await _chargingModuleApi.AssignMaintenanceAsync(id, assignedToUserId);
        if (!assigned)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("NOT_FOUND", "Maintenance issue could not be loaded after assignment.");
        }

        var updated = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, companyId);
        return updated == null
            ? ServiceResult<MaintenanceIssueDto>.Fail("NOT_FOUND", "Maintenance issue could not be loaded after assignment.")
            : ServiceResult<MaintenanceIssueDto>.Ok(MapIssue(updated));
    }

    public async Task<ServiceResult<List<MaintenanceStatusHistoryDto>>> GetStatusHistoryAsync(Guid maintenanceId, Guid companyId)
    {
        if (maintenanceId == Guid.Empty || companyId == Guid.Empty)
        {
            return ServiceResult<List<MaintenanceStatusHistoryDto>>.Fail("VALIDATION", "Maintenance id and company id are required.");
        }

        var issue = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(maintenanceId, companyId);
        if (issue == null)
        {
            return ServiceResult<List<MaintenanceStatusHistoryDto>>.Fail("FORBIDDEN", "Maintenance issue not found or access denied.");
        }

        var entries = await _unitOfWork.AuditLogQueries.GetByEntityAsync(nameof(DomainMaintenance), maintenanceId, companyId);

        var history = entries
            .OrderBy(entry => entry.AtUtc)
            .Select(BllDtoFactory.CreateMaintenanceStatusHistoryDto)
            .ToList();

        return ServiceResult<List<MaintenanceStatusHistoryDto>>.Ok(history);
    }

    private static bool IsValidTransition(DomainMaintenanceStatus from, DomainMaintenanceStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return (from, to) switch
        {
            (DomainMaintenanceStatus.Reported, DomainMaintenanceStatus.InProgress) => true,
            (DomainMaintenanceStatus.Reported, DomainMaintenanceStatus.Resolved) => true,
            (DomainMaintenanceStatus.InProgress, DomainMaintenanceStatus.Resolved) => true,
            _ => false
        };
    }

    private static MaintenanceIssueDto MapIssue(MaintenanceContract issue)
    {
        return new MaintenanceIssueDto
        {
            Id = issue.Id,
            StationId = issue.ChargingStationId,
            StationName = issue.StationName,
            IssueDescription = issue.IssueDescription,
            Status = MapMaintenanceStatus(issue.Status),
            ReportedAtUtc = issue.ReportedAtUtc,
            ResolvedAtUtc = issue.ResolvedAtUtc,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = string.Empty,
            ReporterUserName = string.Empty,
            Notes = issue.Notes
        };
    }

    private static DomainMaintenanceStatus MapMaintenanceStatus(Shared.Contracts.Charging.EMaintenanceStatus status)
    {
        return status switch
        {
            Shared.Contracts.Charging.EMaintenanceStatus.Reported => DomainMaintenanceStatus.Reported,
            Shared.Contracts.Charging.EMaintenanceStatus.InProgress => DomainMaintenanceStatus.InProgress,
            Shared.Contracts.Charging.EMaintenanceStatus.Resolved => DomainMaintenanceStatus.Resolved,
            _ => DomainMaintenanceStatus.Reported
        };
    }

    private static Shared.Contracts.Charging.EMaintenanceStatus MapMaintenanceStatus(DomainMaintenanceStatus status)
    {
        return status switch
        {
            DomainMaintenanceStatus.Reported => Shared.Contracts.Charging.EMaintenanceStatus.Reported,
            DomainMaintenanceStatus.InProgress => Shared.Contracts.Charging.EMaintenanceStatus.InProgress,
            DomainMaintenanceStatus.Resolved => Shared.Contracts.Charging.EMaintenanceStatus.Resolved,
            _ => Shared.Contracts.Charging.EMaintenanceStatus.Reported
        };
    }
}
