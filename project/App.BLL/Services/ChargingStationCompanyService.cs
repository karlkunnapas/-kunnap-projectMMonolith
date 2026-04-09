using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;

namespace App.BLL.Services;

public class ChargingStationCompanyService : IChargingStationCompanyService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public ChargingStationCompanyService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<ServiceResult<List<CompanyStationDto>>> GetCompanyStationsAsync(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<CompanyStationDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var stations = await _unitOfWork.ChargingStations.GetByCompanyAsync(companyId);
        return ServiceResult<List<CompanyStationDto>>.Ok(stations.Select(MapStation).ToList());
    }

    public async Task<ServiceResult<CompanyStationDto>> GetStationDetailsAsync(Guid stationId, Guid companyId)
    {
        if (stationId == Guid.Empty || companyId == Guid.Empty)
        {
            return ServiceResult<CompanyStationDto>.Fail("VALIDATION", "Station id and company id are required.");
        }

        var station = await _unitOfWork.ChargingStations.GetByIdForCompanyAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult<CompanyStationDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        return ServiceResult<CompanyStationDto>.Ok(MapStation(station));
    }

    public async Task<ServiceResult<CompanyStationFormDto>> GetCreateFormAsync(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<CompanyStationFormDto>.Fail("VALIDATION", "Company id is required.");
        }

        var company = await _unitOfWork.Companies.GetByIdAsync(companyId);
        if (company == null)
        {
            return ServiceResult<CompanyStationFormDto>.Fail("FORBIDDEN", "Company not found or access denied.");
        }

        var connectors = await GetConnectorOptionsAsync(Array.Empty<Guid>());

        return ServiceResult<CompanyStationFormDto>.Ok(new CompanyStationFormDto
        {
            CompanyId = companyId,
            Status = EStationStatus.Available,
            IsActive = true,
            AvailableConnectors = connectors
        });
    }

    public async Task<ServiceResult<CompanyStationFormDto>> GetEditFormAsync(Guid stationId, Guid companyId)
    {
        if (stationId == Guid.Empty || companyId == Guid.Empty)
        {
            return ServiceResult<CompanyStationFormDto>.Fail("VALIDATION", "Station id and company id are required.");
        }

        var station = await _unitOfWork.ChargingStations.GetByIdForCompanyAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult<CompanyStationFormDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var selectedConnectorIds = station.ChargingStationConnectors?
            .Select(link => link.ConnectorId)
            .Distinct()
            .ToList() ?? new List<Guid>();

        return ServiceResult<CompanyStationFormDto>.Ok(new CompanyStationFormDto
        {
            Id = station.Id,
            CompanyId = companyId,
            NameEn = station.Name.Translate("en") ?? string.Empty,
            NameEt = station.Name.Translate("et") ?? string.Empty,
            Location = station.Location,
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            Status = NormalizeStationStatus(station.Status),
            IsActive = station.IsActive,
            SelectedConnectorIds = selectedConnectorIds,
            AvailableConnectors = await GetConnectorOptionsAsync(selectedConnectorIds)
        });
    }

    public async Task<ServiceResult<CompanyStationDto>> CreateStationAsync(Guid companyId, Guid userId, string userName, CompanyStationUpsertDto dto)
    {
        var identityValidation = ValidateActorContextForDto(companyId, userId);
        if (identityValidation != null)
        {
            return identityValidation;
        }

        var validationErrors = ValidateUpsertDto(dto);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<CompanyStationDto>.Fail(validationErrors);
        }

        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = BuildStationName(dto.NameEn, dto.NameEt),
            Location = dto.Location.Trim(),
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            Status = NormalizeStationStatus(dto.Status),
            IsActive = dto.IsActive
        };

        await _unitOfWork.ChargingStations.CreateForCompanyAsync(station, companyId);
        await SyncConnectorAssignmentsAsync(station.Id, dto.SelectedConnectorIds);
        await _unitOfWork.SaveAsync();

        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(ChargingStation),
            station.Id,
            "Create");

        var created = await _unitOfWork.ChargingStations.GetByIdForCompanyAsync(station.Id, companyId);
        return created == null
            ? ServiceResult<CompanyStationDto>.Fail("NOT_FOUND", "Charging station could not be loaded after creation.")
            : ServiceResult<CompanyStationDto>.Ok(MapStation(created));
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

        var validationErrors = ValidateUpsertDto(dto);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<CompanyStationDto>.Fail(validationErrors);
        }

        var station = await _unitOfWork.ChargingStations.GetByIdForCompanyAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult<CompanyStationDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        station.Name = BuildStationName(dto.NameEn, dto.NameEt);
        station.Location = dto.Location.Trim();
        station.PricePerKwh = dto.PricePerKwh;
        station.MaxPower = dto.MaxPower;
        station.Status = NormalizeStationStatus(dto.Status);
        station.IsActive = dto.IsActive;

        _unitOfWork.ChargingStations.UpdateForCompany(station);
        await SyncConnectorAssignmentsAsync(station.Id, dto.SelectedConnectorIds);
        await _unitOfWork.SaveAsync();

        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(ChargingStation),
            station.Id,
            "Update");

        var updated = await _unitOfWork.ChargingStations.GetByIdForCompanyAsync(stationId, companyId);
        return updated == null
            ? ServiceResult<CompanyStationDto>.Fail("NOT_FOUND", "Charging station could not be loaded after update.")
            : ServiceResult<CompanyStationDto>.Ok(MapStation(updated));
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

        var station = await _unitOfWork.ChargingStations.GetByIdForCompanyAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var hasReservations = station.Reservations?.Any() == true;
        var hasSessions = station.ChargingSessions?.Any() == true;
        var hasMaintenance = station.MaintenanceIssues?.Any() == true;

        if (hasReservations || hasSessions || hasMaintenance)
        {
            return ServiceResult.Fail("VALIDATION", "Station cannot be deleted because it has dependent records.");
        }

        var links = _unitOfWork.ChargingStationConnectors.GetQueryable()
            .Where(link => link.ChargingStationId == stationId)
            .ToList();

        foreach (var link in links)
        {
            _unitOfWork.ChargingStationConnectors.Remove(link);
        }

        _unitOfWork.ChargingStations.DeleteForCompany(station);
        await _unitOfWork.SaveAsync();

        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(ChargingStation),
            station.Id,
            "Delete");

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<CompanyStationDto>> UpdateStatusAsync(
        Guid stationId,
        Guid companyId,
        Guid userId,
        string userName,
        EStationStatus status)
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

        var station = await _unitOfWork.ChargingStations.GetByIdForCompanyAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult<CompanyStationDto>.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        station.Status = NormalizeStationStatus(status);
        _unitOfWork.ChargingStations.UpdateForCompany(station);
        await _unitOfWork.SaveAsync();

        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(ChargingStation),
            station.Id,
            "StatusTransition",
            $"{{\"status\":\"{station.Status}\"}}");

        return ServiceResult<CompanyStationDto>.Ok(MapStation(station));
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

        var station = await _unitOfWork.ChargingStations.GetByIdForCompanyAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var connector = await _unitOfWork.Connectors.GetByIdAsync(connectorId);
        if (connector == null || !connector.IsActive)
        {
            return ServiceResult.Fail("NOT_FOUND", "Connector was not found.");
        }

        var existingLink = _unitOfWork.ChargingStationConnectors.GetQueryable()
            .FirstOrDefault(link => link.ChargingStationId == stationId && link.ConnectorId == connectorId);

        if (existingLink != null)
        {
            return ServiceResult.Fail("VALIDATION", "Connector is already assigned.");
        }

        await _unitOfWork.ChargingStationConnectors.AddAsync(new ChargingStationConnector
        {
            Id = Guid.NewGuid(),
            ChargingStationId = stationId,
            ConnectorId = connectorId
        });

        await _unitOfWork.SaveAsync();
        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(ChargingStationConnector),
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

        var station = await _unitOfWork.ChargingStations.GetByIdForCompanyAsync(stationId, companyId);
        if (station == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Charging station not found or access denied.");
        }

        var existingLink = _unitOfWork.ChargingStationConnectors.GetQueryable()
            .FirstOrDefault(link => link.ChargingStationId == stationId && link.ConnectorId == connectorId);

        if (existingLink == null)
        {
            return ServiceResult.Fail("NOT_FOUND", "Connector assignment not found.");
        }

        _unitOfWork.ChargingStationConnectors.Remove(existingLink);
        await _unitOfWork.SaveAsync();
        await _auditService.LogMutationAsync(
            companyId,
            userName,
            nameof(ChargingStationConnector),
            stationId,
            "RemoveConnector",
            $"{{\"connectorId\":\"{connectorId}\"}}");

        return ServiceResult.Ok();
    }

    private Task<List<CompanyStationConnectorOptionDto>> GetConnectorOptionsAsync(IReadOnlyCollection<Guid> selectedConnectorIds)
    {
        var connectors = _unitOfWork.Connectors.GetQueryable()
            .Where(connector => connector.IsActive)
            .ToList()
            .Select(connector => new CompanyStationConnectorOptionDto
            {
                ConnectorId = connector.Id,
                ConnectorName = connector.Name.Translate() ?? connector.Name.ToString() ?? string.Empty,
                IsAssigned = selectedConnectorIds.Contains(connector.Id)
            })
            .OrderBy(connector => connector.ConnectorName)
            .ToList();

        return Task.FromResult(connectors);
    }

    private List<ServiceError> ValidateUpsertDto(CompanyStationUpsertDto dto)
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

        var existingConnectors = _unitOfWork.Connectors.GetQueryable()
            .Where(connector => connector.IsActive && selectedConnectorIds.Contains(connector.Id))
            .Select(connector => connector.Id)
            .ToList();

        if (existingConnectors.Count != selectedConnectorIds.Count)
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "One or more selected connectors are invalid." });
        }

        return errors;
    }

    private async Task SyncConnectorAssignmentsAsync(Guid stationId, IReadOnlyCollection<Guid> selectedConnectorIds)
    {
        var normalizedSelectedIds = selectedConnectorIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var currentLinks = _unitOfWork.ChargingStationConnectors.GetQueryable()
            .Where(link => link.ChargingStationId == stationId)
            .ToList();

        foreach (var staleLink in currentLinks.Where(link => !normalizedSelectedIds.Contains(link.ConnectorId)))
        {
            _unitOfWork.ChargingStationConnectors.Remove(staleLink);
        }

        var currentConnectorIds = currentLinks.Select(link => link.ConnectorId).ToHashSet();
        foreach (var connectorId in normalizedSelectedIds.Where(id => !currentConnectorIds.Contains(id)))
        {
            await _unitOfWork.ChargingStationConnectors.AddAsync(new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = stationId,
                ConnectorId = connectorId
            });
        }
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

    private static LangStr BuildStationName(string nameEn, string nameEt)
    {
        var normalizedEn = (nameEn ?? string.Empty).Trim();
        var normalizedEt = (nameEt ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedEn))
        {
            normalizedEn = normalizedEt;
        }

        if (string.IsNullOrWhiteSpace(normalizedEt))
        {
            normalizedEt = normalizedEn;
        }

        var langStr = new LangStr();
        langStr.SetTranslation(normalizedEn, "en");
        langStr.SetTranslation(normalizedEt, "et");
        return langStr;
    }

    private static CompanyStationDto MapStation(ChargingStation station)
    {
        return new CompanyStationDto
        {
            Id = station.Id,
            Name = station.Name.Translate() ?? station.Name.ToString() ?? string.Empty,
            Location = station.Location,
            Status = station.Status,
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            IsActive = station.IsActive,
            Connectors = station.ChargingStationConnectors?
                .Where(link => link.Connector != null && link.Connector.IsActive)
                .Select(link => link.Connector!.Name.Translate() ?? link.Connector.Name.ToString() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct()
                .OrderBy(value => value)
                .ToList() ?? new List<string>(),
            MaintenanceIssueCount = station.MaintenanceIssues?.Count(issue => issue.Status != EMaintenanceStatus.Resolved) ?? 0
        };
    }

    private static EStationStatus NormalizeStationStatus(EStationStatus status)
    {
        var rawValue = (int)status;
        if (rawValue == 1)
        {
            return EStationStatus.InUse;
        }

        return Enum.IsDefined(typeof(EStationStatus), status)
            ? status
            : EStationStatus.Available;
    }

    private static bool IsValidStationStatus(EStationStatus status)
    {
        var rawValue = (int)status;
        return rawValue == 1 || Enum.IsDefined(typeof(EStationStatus), status);
    }
}
