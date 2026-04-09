using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;

namespace App.BLL.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IUnitOfWork _unitOfWork;

    public MaintenanceService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<List<MaintenanceIssueDto>>> GetIssuesAsync(Guid companyId, bool includeResolved = true)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<MaintenanceIssueDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var issues = await _unitOfWork.Maintenances.GetByCompanyAsync(companyId, includeResolved);
        return ServiceResult<List<MaintenanceIssueDto>>.Ok(issues.Select(MapIssue).ToList());
    }

    public async Task<ServiceResult<MaintenanceIssueDto>> GetByIdAsync(Guid id, Guid companyId)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("VALIDATION", "Issue id and company id are required.");
        }

        var issue = await _unitOfWork.Maintenances.GetByIdForCompanyAsync(id, companyId);
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

        var station = await _unitOfWork.ChargingStations.GetByIdForCompanyAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var issue = new Maintenance
        {
            Id = Guid.NewGuid(),
            ChargingStationId = stationId,
            ReportedByUserId = userId,
            IssueDescription = issueDescription.Trim(),
            Status = EMaintenanceStatus.Reported,
            ReportedAt = DateTime.UtcNow
        };

        await _unitOfWork.Maintenances.AddAsync(issue);
        await _unitOfWork.SaveAsync();

        var created = await _unitOfWork.Maintenances.GetByIdForCompanyAsync(issue.Id, companyId);
        return created == null
            ? ServiceResult<MaintenanceIssueDto>.Fail("NOT_FOUND", "Maintenance issue could not be loaded after creation.")
            : ServiceResult<MaintenanceIssueDto>.Ok(MapIssue(created));
    }

    public async Task<ServiceResult<MaintenanceIssueDto>> UpdateStatusAsync(
        Guid id,
        Guid companyId,
        Guid userId,
        EMaintenanceStatus newStatus,
        string? notes)
    {
        if (id == Guid.Empty || companyId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("VALIDATION", "Issue id, company id and user id are required.");
        }

        var issue = await _unitOfWork.Maintenances.GetByIdForCompanyAsync(id, companyId);
        if (issue == null)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("FORBIDDEN", "Maintenance issue not found or access denied.");
        }

        if (!IsValidTransition(issue.Status, newStatus))
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("VALIDATION", "Invalid maintenance status transition.");
        }

        issue.Status = newStatus;
        issue.Notes = string.IsNullOrWhiteSpace(notes) ? issue.Notes : notes.Trim();

        if (newStatus == EMaintenanceStatus.Resolved)
        {
            issue.ResolvedAt = DateTime.UtcNow;
        }
        else
        {
            issue.ResolvedAt = null;
        }

        var station = issue.ChargingStation;
        if (station == null || station.CompanyId != companyId)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var targetStationStatus = newStatus == EMaintenanceStatus.Resolved
            ? EStationStatus.Available
            : EStationStatus.Maintenance;
        var stationUpdate = BuildStationStatusUpdate(station, targetStationStatus);
        _unitOfWork.ChargingStations.UpdateForCompany(stationUpdate);

        _unitOfWork.Maintenances.Update(issue);
        await _unitOfWork.SaveAsync();

        return ServiceResult<MaintenanceIssueDto>.Ok(MapIssue(issue));
    }

    public async Task<ServiceResult<MaintenanceIssueDto>> AssignAsync(Guid id, Guid companyId, Guid userId, Guid? assignedToUserId)
    {
        if (id == Guid.Empty || companyId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("VALIDATION", "Issue id, company id and user id are required.");
        }

        var issue = await _unitOfWork.Maintenances.GetByIdForCompanyAsync(id, companyId);
        if (issue == null)
        {
            return ServiceResult<MaintenanceIssueDto>.Fail("FORBIDDEN", "Maintenance issue not found or access denied.");
        }

        issue.AssignedToUserId = assignedToUserId;
        _unitOfWork.Maintenances.Update(issue);
        await _unitOfWork.SaveAsync();

        var updated = await _unitOfWork.Maintenances.GetByIdForCompanyAsync(id, companyId);
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

        var issue = await _unitOfWork.Maintenances.GetByIdForCompanyAsync(maintenanceId, companyId);
        if (issue == null)
        {
            return ServiceResult<List<MaintenanceStatusHistoryDto>>.Fail("FORBIDDEN", "Maintenance issue not found or access denied.");
        }

        var entries = await _unitOfWork.AuditLogQueries.GetByEntityAsync(nameof(Maintenance), maintenanceId, companyId);

        var history = entries
            .OrderBy(entry => entry.AtUtc)
            .Select(entry => new MaintenanceStatusHistoryDto
            {
                AtUtc = entry.AtUtc,
                Action = entry.Action,
                Actor = entry.UserName,
                Changes = entry.ChangesJson ?? string.Empty
            })
            .ToList();

        return ServiceResult<List<MaintenanceStatusHistoryDto>>.Ok(history);
    }

    private static bool IsValidTransition(EMaintenanceStatus from, EMaintenanceStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return (from, to) switch
        {
            (EMaintenanceStatus.Reported, EMaintenanceStatus.InProgress) => true,
            (EMaintenanceStatus.Reported, EMaintenanceStatus.Resolved) => true,
            (EMaintenanceStatus.InProgress, EMaintenanceStatus.Resolved) => true,
            _ => false
        };
    }

    private static MaintenanceIssueDto MapIssue(Maintenance issue)
    {
        return new MaintenanceIssueDto
        {
            Id = issue.Id,
            StationId = issue.ChargingStationId,
            StationName = issue.ChargingStation?.Name.Translate() ?? issue.ChargingStation?.Name.ToString() ?? string.Empty,
            IssueDescription = issue.IssueDescription,
            Status = issue.Status,
            ReportedAtUtc = issue.ReportedAt,
            ResolvedAtUtc = issue.ResolvedAt,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = issue.AssignedToUser?.UserName ?? string.Empty,
            ReporterUserName = issue.ReportedByUser?.UserName ?? string.Empty,
            Notes = issue.Notes
        };
    }

    private static ChargingStation BuildStationStatusUpdate(ChargingStation source, EStationStatus status)
    {
        return new ChargingStation
        {
            Id = source.Id,
            Name = source.Name,
            Location = source.Location,
            Status = status,
            PricePerKwh = source.PricePerKwh,
            MaxPower = source.MaxPower,
            IsActive = source.IsActive,
            CompanyId = source.CompanyId
        };
    }
}
