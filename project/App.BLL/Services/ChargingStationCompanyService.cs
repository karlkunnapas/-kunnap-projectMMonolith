using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using DomainStationStatus = App.Domain.EStationStatus;
using DomainChargingStation = App.Domain.ChargingStation;
using DomainChargingStationConnector = App.Domain.ChargingStationConnector;

namespace App.BLL.Services;

public class ChargingStationCompanyService : IChargingStationCompanyService
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private readonly IAuditService _auditService;

    public ChargingStationCompanyService(
        IChargingModuleApi chargingModuleApi,
        ICompaniesModuleApi companiesModuleApi,
        IAuditService auditService)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
        _auditService = auditService;
    }

    public async Task<ServiceResult<List<CompanyStationDto>>> GetCompanyStationsAsync(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<CompanyStationDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var stations = await _chargingModuleApi.GetCompanyStationsAsync(companyId);
        var issues = await _chargingModuleApi.GetMaintenancesByCompanyAsync(companyId, includeResolved: false);
        var issueCountByStation = issues.GroupBy(x => x.ChargingStationId).ToDictionary(g => g.Key, g => g.Count());

        return ServiceResult<List<CompanyStationDto>>.Ok(stations.Select(station =>
        {
            var dto = MapStation(station);
            dto.MaintenanceIssueCount = issueCountByStation.TryGetValue(station.Id, out var count) ? count : 0;
            return dto;
        }).ToList());
    }

    public async Task<ServiceResult<CompanyStationDto>> GetStationDetailsAsync(Guid stationId, Guid companyId)
    {
        if (stationId == Guid.Empty || companyId == Guid.Empty)
        {
            return ServiceResult<CompanyStationDto>.Fail("VALIDATION", "Station id and company id are required.");
        }

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult<CompanyStationDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var dto = MapStation(station);
        dto.MaintenanceIssueCount = (await _chargingModuleApi.GetMaintenancesByCompanyAsync(companyId, includeResolved: false))
            .Count(x => x.ChargingStationId == stationId);
        return ServiceResult<CompanyStationDto>.Ok(dto);
    }

    public async Task<ServiceResult<CompanyStationFormDto>> GetCreateFormAsync(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<CompanyStationFormDto>.Fail("VALIDATION", "Company id is required.");
        }

        var companyExists = await _companiesModuleApi.CompanyExistsAsync(companyId);
        if (!companyExists)
        {
            return ServiceResult<CompanyStationFormDto>.Fail("FORBIDDEN", "Company not found or access denied.");
        }

        var connectors = await GetConnectorOptionsAsync(Array.Empty<Guid>());

        return ServiceResult<CompanyStationFormDto>.Ok(BllDtoFactory.CreateCompanyStationFormDto(
            id: null,
            companyId: companyId,
            nameEn: string.Empty,
            nameEt: string.Empty,
            location: string.Empty,
            pricePerKwh: 0m,
            maxPower: 0m,
            status: DomainStationStatus.Available,
            isActive: true,
            selectedConnectorIds: new List<Guid>(),
            availableConnectors: connectors));
    }

    public async Task<ServiceResult<CompanyStationFormDto>> GetEditFormAsync(Guid stationId, Guid companyId)
    {
        if (stationId == Guid.Empty || companyId == Guid.Empty)
        {
            return ServiceResult<CompanyStationFormDto>.Fail("VALIDATION", "Station id and company id are required.");
        }

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult<CompanyStationFormDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var selectedConnectorIds = (await _chargingModuleApi.GetStationAssignedConnectorIdsAsync(stationId)).Distinct().ToList();

        return ServiceResult<CompanyStationFormDto>.Ok(BllDtoFactory.CreateCompanyStationFormDto(
            id: station.Id,
            companyId: companyId,
            nameEn: station.Name,
            nameEt: station.Name,
            location: station.Location,
            pricePerKwh: station.PricePerKwh,
            maxPower: station.MaxPower,
            status: NormalizeStationStatus(MapStationStatus(station.Status)),
            isActive: station.IsActive,
            selectedConnectorIds: selectedConnectorIds,
            availableConnectors: await GetConnectorOptionsAsync(selectedConnectorIds)));
    }

    public async Task<ServiceResult<CompanyStationDto>> CreateStationAsync(Guid companyId, Guid userId, string userName, CompanyStationUpsertDto dto)
    {
        var identityValidation = ValidateActorContextForDto(companyId, userId);
        if (identityValidation != null)
        {
            return identityValidation;
        }

        var validationErrors = await ValidateUpsertDtoAsync(dto);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<CompanyStationDto>.Fail(validationErrors);
        }

        var created = await _chargingModuleApi.CreateCompanyStationAsync(new UpsertCompanyStationContract
        {
            StationId = Guid.NewGuid(),
            CompanyId = companyId,
            NameEn = dto.NameEn,
            NameEt = dto.NameEt,
            Location = dto.Location.Trim(),
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            Status = MapStationStatus(NormalizeStationStatus(dto.Status)),
            IsActive = dto.IsActive
        });

        await _chargingModuleApi.SetStationConnectorsAsync(created.Id, dto.SelectedConnectorIds);

        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(App.Domain.ChargingStation),
            created.Id,
            "Create");

        var persisted = await _chargingModuleApi.GetCompanyStationByIdAsync(created.Id, companyId);
        return persisted == null
            ? ServiceResult<CompanyStationDto>.Fail("NOT_FOUND", "Charging station could not be loaded after creation.")
            : ServiceResult<CompanyStationDto>.Ok(MapStation(persisted));
    }

    public async Task<ServiceResult<CompanyStationDto>> UpdateStationAsync(
        Guid stationId,
        Guid companyId,
        Guid userId,
        string userName,
        CompanyStationUpsertDto dto)
    {
        var identityValidation = ValidateActorContextForDto(companyId, userId);
        if (identityValidation != null)
        {
            return identityValidation;
        }

        if (stationId == Guid.Empty)
        {
            return ServiceResult<CompanyStationDto>.Fail("VALIDATION", "Station id is required.");
        }

        var validationErrors = await ValidateUpsertDtoAsync(dto);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<CompanyStationDto>.Fail(validationErrors);
        }

        var updated = await _chargingModuleApi.UpdateCompanyStationAsync(new UpsertCompanyStationContract
        {
            StationId = stationId,
            CompanyId = companyId,
            NameEn = dto.NameEn,
            NameEt = dto.NameEt,
            Location = dto.Location.Trim(),
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            Status = MapStationStatus(NormalizeStationStatus(dto.Status)),
            IsActive = dto.IsActive
        });

        if (updated == null)
        {
            return ServiceResult<CompanyStationDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        await _chargingModuleApi.SetStationConnectorsAsync(stationId, dto.SelectedConnectorIds);

        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(App.Domain.ChargingStation),
            stationId,
            "Update");

        var persisted = await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, companyId);
        return persisted == null
            ? ServiceResult<CompanyStationDto>.Fail("NOT_FOUND", "Charging station could not be loaded after update.")
            : ServiceResult<CompanyStationDto>.Ok(MapStation(persisted));
    }

    public async Task<ServiceResult> DeleteStationAsync(Guid stationId, Guid companyId, Guid userId, string userName)
    {
        var identityValidation = ValidateActorContext(companyId, userId);
        if (identityValidation != null)
        {
            return identityValidation;
        }

        if (stationId == Guid.Empty)
        {
            return ServiceResult.Fail("VALIDATION", "Station id is required.");
        }

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var deleted = await _chargingModuleApi.DeleteCompanyStationAsync(stationId, companyId);
        if (!deleted)
        {
            return ServiceResult.Fail("VALIDATION", "Station cannot be deleted because it has dependent records.");
        }

        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(App.Domain.ChargingStation),
            stationId,
            "Delete");

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<CompanyStationDto>> UpdateStatusAsync(
        Guid stationId,
        Guid companyId,
        Guid userId,
        string userName,
        DomainStationStatus status)
    {
        var identityValidation = ValidateActorContextForDto(companyId, userId);
        if (identityValidation != null)
        {
            return identityValidation;
        }

        if (stationId == Guid.Empty)
        {
            return ServiceResult<CompanyStationDto>.Fail("VALIDATION", "Station id is required.");
        }

        if (!IsValidStationStatus(status))
        {
            return ServiceResult<CompanyStationDto>.Fail("VALIDATION", "Invalid station status.");
        }

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult<CompanyStationDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var updated = await _chargingModuleApi.UpdateStationStatusAsync(stationId, MapStationStatus(NormalizeStationStatus(status)));
        if (!updated)
        {
            return ServiceResult<CompanyStationDto>.Fail("NOT_FOUND", "Charging station not found.");
        }

        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(App.Domain.ChargingStation),
            stationId,
            "StatusTransition",
            $"{{\"status\":\"{NormalizeStationStatus(status)}\"}}");

        var persisted = await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, companyId);
        return persisted == null
            ? ServiceResult<CompanyStationDto>.Fail("NOT_FOUND", "Charging station not found.")
            : ServiceResult<CompanyStationDto>.Ok(MapStation(persisted));
    }

    public async Task<ServiceResult> AssignConnectorAsync(
        Guid stationId,
        Guid companyId,
        Guid userId,
        string userName,
        Guid connectorId)
    {
        var identityValidation = ValidateActorContext(companyId, userId);
        if (identityValidation != null)
        {
            return identityValidation;
        }

        var scopeValidation = ValidateStationConnectorInput(stationId, connectorId);
        if (scopeValidation != null)
        {
            return scopeValidation;
        }

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var connector = (await _chargingModuleApi.GetConnectorsAsync()).FirstOrDefault(c => c.Id == connectorId && c.IsActive);
        if (connector == null)
        {
            return ServiceResult.Fail("NOT_FOUND", "Connector was not found.");
        }

        var assigned = (await _chargingModuleApi.GetStationAssignedConnectorIdsAsync(stationId)).ToHashSet();
        if (assigned.Contains(connectorId))
        {
            return ServiceResult.Fail("VALIDATION", "Connector is already assigned.");
        }

        assigned.Add(connectorId);
        await _chargingModuleApi.SetStationConnectorsAsync(stationId, assigned.ToList());

        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(App.Domain.ChargingStationConnector),
            stationId,
            "AssignConnector",
            $"{{\"connectorId\":\"{connectorId}\"}}");

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveConnectorAsync(
        Guid stationId,
        Guid companyId,
        Guid userId,
        string userName,
        Guid connectorId)
    {
        var identityValidation = ValidateActorContext(companyId, userId);
        if (identityValidation != null)
        {
            return identityValidation;
        }

        var scopeValidation = ValidateStationConnectorInput(stationId, connectorId);
        if (scopeValidation != null)
        {
            return scopeValidation;
        }

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var assigned = (await _chargingModuleApi.GetStationAssignedConnectorIdsAsync(stationId)).ToHashSet();
        if (!assigned.Contains(connectorId))
        {
            return ServiceResult.Fail("NOT_FOUND", "Connector assignment not found.");
        }

        assigned.Remove(connectorId);
        await _chargingModuleApi.SetStationConnectorsAsync(stationId, assigned.ToList());

        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(App.Domain.ChargingStationConnector),
            stationId,
            "RemoveConnector",
            $"{{\"connectorId\":\"{connectorId}\"}}");

        return ServiceResult.Ok();
    }

    private async Task<List<CompanyStationConnectorOptionDto>> GetConnectorOptionsAsync(IReadOnlyCollection<Guid> selectedConnectorIds)
    {
        var connectors = await _chargingModuleApi.GetConnectorsAsync();
        return connectors
            .Select(connector => new CompanyStationConnectorOptionDto
            {
                ConnectorId = connector.Id,
                ConnectorName = connector.Name,
                IsAssigned = selectedConnectorIds.Contains(connector.Id)
            })
            .OrderBy(x => x.ConnectorName)
            .ToList();
    }

    private async Task<List<ServiceError>> ValidateUpsertDtoAsync(CompanyStationUpsertDto dto)
    {
        var errors = new List<ServiceError>();
        var normalizedEn = (dto.NameEn ?? string.Empty).Trim();
        var normalizedEt = (dto.NameEt ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedEn) && string.IsNullOrWhiteSpace(normalizedEt))
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "At least one station name translation is required." });
        }

        if (string.IsNullOrWhiteSpace(dto.Location))
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "Location is required." });
        }

        if (dto.PricePerKwh <= 0)
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "Price per kWh must be greater than zero." });
        }

        if (dto.MaxPower <= 0)
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "Max power must be greater than zero." });
        }

        if (!IsValidStationStatus(dto.Status))
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "Invalid station status." });
        }

        var selectedConnectorIds = dto.SelectedConnectorIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var activeConnectorIds = (await _chargingModuleApi.GetConnectorsAsync())
            .Where(c => c.IsActive)
            .Select(c => c.Id)
            .ToHashSet();

        if (selectedConnectorIds.Any(id => !activeConnectorIds.Contains(id)))
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "One or more selected connectors are invalid." });
        }

        return errors;
    }

    private static ServiceResult<CompanyStationDto>? ValidateActorContextForDto(Guid companyId, Guid userId)
    {
        if (companyId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult<CompanyStationDto>.Fail("VALIDATION", "Company and user identifiers are required.");
        }

        return null;
    }

    private static ServiceResult? ValidateActorContext(Guid companyId, Guid userId)
    {
        if (companyId == Guid.Empty || userId == Guid.Empty)
        {
            return ServiceResult.Fail("VALIDATION", "Company and user identifiers are required.");
        }

        return null;
    }

    private static ServiceResult? ValidateStationConnectorInput(Guid stationId, Guid connectorId)
    {
        if (stationId == Guid.Empty || connectorId == Guid.Empty)
        {
            return ServiceResult.Fail("VALIDATION", "Station and connector identifiers are required.");
        }

        return null;
    }

    private static CompanyStationDto MapStation(ChargingStationContract station)
    {
        return new CompanyStationDto
        {
            Id = station.Id,
            Name = station.Name,
            Location = station.Location,
            Status = MapStationStatus(station.Status),
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            IsActive = station.IsActive,
            Connectors = station.Connectors
                .Where(c => c.IsActive)
                .Select(c => c.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList(),
            MaintenanceIssueCount = 0
        };
    }

    private static DomainStationStatus NormalizeStationStatus(DomainStationStatus status)
    {
        var rawValue = (int)status;
        if (rawValue == 1)
        {
            return DomainStationStatus.InUse;
        }

        return Enum.IsDefined(typeof(DomainStationStatus), status)
            ? status
            : DomainStationStatus.Available;
    }

    private static bool IsValidStationStatus(DomainStationStatus status)
    {
        var rawValue = (int)status;
        return rawValue == 1 || Enum.IsDefined(typeof(DomainStationStatus), status);
    }

    private static DomainStationStatus MapStationStatus(Shared.Contracts.Charging.EStationStatus status)
    {
        return status switch
        {
            Shared.Contracts.Charging.EStationStatus.Available => DomainStationStatus.Available,
            Shared.Contracts.Charging.EStationStatus.InUse => DomainStationStatus.InUse,
            Shared.Contracts.Charging.EStationStatus.Maintenance => DomainStationStatus.Maintenance,
            _ => DomainStationStatus.Available
        };
    }

    private static Shared.Contracts.Charging.EStationStatus MapStationStatus(DomainStationStatus status)
    {
        return status switch
        {
            DomainStationStatus.Available => Shared.Contracts.Charging.EStationStatus.Available,
            DomainStationStatus.InUse => Shared.Contracts.Charging.EStationStatus.InUse,
            DomainStationStatus.Maintenance => Shared.Contracts.Charging.EStationStatus.Maintenance,
            _ => Shared.Contracts.Charging.EStationStatus.Available
        };
    }
}
