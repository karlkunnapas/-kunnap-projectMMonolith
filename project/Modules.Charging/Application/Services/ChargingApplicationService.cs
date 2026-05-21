using Mediator;
using Modules.Charging.Application.Mappers;
using Modules.Charging.Infrastructure;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies.Mediator;

namespace Modules.Charging.Application.Services;

internal sealed class ChargingApplicationService : IChargingApplicationService
{
    private readonly IChargingRepository _chargingRepository;
    private readonly IMediator _mediator;

    public ChargingApplicationService(
        IChargingRepository chargingRepository,
        IMediator mediator)
    {
        _chargingRepository = chargingRepository;
        _mediator = mediator;
    }

    public async Task<IReadOnlyCollection<ChargingStationContract>> GetStationsForHomeAsync(
        EStationStatus? status = null,
        CancellationToken ct = default)
    {
        var stations = await _chargingRepository.GetStationsForHomeAsync(status, ct);
        var companyIds = stations
            .Where(x => x.CompanyId.HasValue)
            .Select(x => x.CompanyId!.Value)
            .Distinct()
            .ToList();

        var companyActiveMap = new Dictionary<Guid, bool>(companyIds.Count);
        foreach (var companyId in companyIds)
        {
            companyActiveMap[companyId] = await _mediator.Send(new IsCompanyActiveQuery(companyId), ct);
        }

        return stations
            .Where(station => !station.CompanyId.HasValue || companyActiveMap.GetValueOrDefault(station.CompanyId.Value))
            .Select(ChargingContractMapper.ToContract)
            .ToList();
    }

    public async Task<IReadOnlyCollection<AdminChargingStationContract>> GetStationsForAdminAsync(
        CancellationToken ct = default)
    {
        var stations = await _chargingRepository.GetStationsForAdminAsync(ct);
        return stations
            .Select(ChargingContractMapper.ToContract)
            .ToList();
    }

    public async Task<ChargingStationContract?> GetStationByIdAsync(
        Guid stationId,
        CancellationToken ct = default)
    {
        var dto = await _chargingRepository.GetStationByIdAsync(stationId, ct);
        return dto is { } ? ChargingContractMapper.ToContract(dto) : null;
    }

    public Task<Guid?> GetStationCompanyIdAsync(Guid stationId, CancellationToken ct = default)
    {
        return _chargingRepository.GetStationCompanyIdAsync(stationId, ct);
    }

    public Task<IReadOnlyCollection<Guid>> GetStationConnectorIdsAsync(Guid stationId, CancellationToken ct = default)
    {
        return _chargingRepository.GetStationConnectorIdsAsync(stationId, ct);
    }

    public async Task<IReadOnlyCollection<ChargingStationContract>> GetCompanyStationsAsync(
        Guid companyId,
        CancellationToken ct = default)
    {
        var stations = await _chargingRepository.GetCompanyStationsAsync(companyId, ct);
        return stations
            .Select(ChargingContractMapper.ToContract)
            .ToList();
    }

    public async Task<ChargingStationContract?> GetCompanyStationByIdAsync(
        Guid stationId,
        Guid companyId,
        CancellationToken ct = default)
    {
        var dto = await _chargingRepository.GetCompanyStationByIdAsync(stationId, companyId, ct);
        return dto is { } ? ChargingContractMapper.ToContract(dto) : null;
    }

    public async Task<CompanyDashboardStatsContract> GetCompanyDashboardStatsAsync(
        Guid companyId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var stats = await _chargingRepository.GetCompanyDashboardStatsAsync(companyId, fromUtc, toUtc, ct);
        return ChargingContractMapper.ToContract(stats);
    }

    public async Task<IReadOnlyCollection<ChargingSessionContract>> GetCompanyChargingSessionsAsync(
        Guid companyId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var sessions = await _chargingRepository.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc, ct);
        return sessions
            .Select(ChargingContractMapper.ToContract)
            .ToList();
    }

    public async Task<IReadOnlyCollection<ReservationContract>> GetCompanyReservationsAsync(
        Guid companyId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var reservations = await _chargingRepository.GetCompanyReservationsAsync(companyId, fromUtc, toUtc, ct);
        return reservations
            .Select(ChargingContractMapper.ToContract)
            .ToList();
    }

    public Task<int> GetReservationCountByRangeAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        return _chargingRepository.GetReservationCountByRangeAsync(fromUtc, toUtc, ct);
    }

    public async Task<ChargingStationContract> CreateCompanyStationAsync(
        UpsertCompanyStationContract request,
        CancellationToken ct = default)
    {
        var dto = ChargingContractMapper.ToDto(request);
        var station = await _chargingRepository.CreateCompanyStationAsync(dto, ct);
        return ChargingContractMapper.ToContract(station);
    }

    public async Task<ChargingStationContract?> UpdateCompanyStationAsync(
        UpsertCompanyStationContract request,
        CancellationToken ct = default)
    {
        var dto = ChargingContractMapper.ToDto(request);
        var station = await _chargingRepository.UpdateCompanyStationAsync(dto, ct);
        return station is { } ? ChargingContractMapper.ToContract(station) : null;
    }

    public Task<bool> DeleteCompanyStationAsync(Guid stationId, Guid companyId, CancellationToken ct = default)
    {
        return _chargingRepository.DeleteCompanyStationAsync(stationId, companyId, ct);
    }

    public async Task<ChargingStationContract?> SetCompanyStationActivationAsync(
        Guid stationId,
        Guid companyId,
        bool isActive,
        CancellationToken ct = default)
    {
        var station = await _chargingRepository.SetCompanyStationActivationAsync(stationId, companyId, isActive, ct);
        return station is { } ? ChargingContractMapper.ToContract(station) : null;
    }

    public Task<IReadOnlyCollection<Guid>> GetStationAssignedConnectorIdsAsync(
        Guid stationId,
        CancellationToken ct = default)
    {
        return _chargingRepository.GetStationAssignedConnectorIdsAsync(stationId, ct);
    }

    public Task SetStationConnectorsAsync(
        Guid stationId,
        IReadOnlyCollection<Guid> connectorIds,
        CancellationToken ct = default)
    {
        return _chargingRepository.SetStationConnectorsAsync(stationId, connectorIds, ct);
    }

    public async Task<IReadOnlyCollection<ConnectorContract>> GetConnectorsAsync(
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        var connectors = await _chargingRepository.GetConnectorsAsync(includeInactive, ct);
        return connectors
            .Select(ChargingContractMapper.ToContract)
            .ToList();
    }

    public async Task<ConnectorTypeContract?> GetConnectorTypeByIdAsync(
        Guid connectorTypeId,
        CancellationToken ct = default)
    {
        var dto = await _chargingRepository.GetConnectorTypeByIdAsync(connectorTypeId, ct);
        return dto is { } ? ChargingContractMapper.ToContract(dto) : null;
    }

    public async Task<ConnectorTypeContract> CreateConnectorTypeAsync(
        string nameEn,
        string nameEt,
        bool isActive,
        CancellationToken ct = default)
    {
        var connector = await _chargingRepository.CreateConnectorTypeAsync(nameEn, nameEt, isActive, ct);
        return ChargingContractMapper.ToContract(connector);
    }

    public async Task<ConnectorTypeContract?> UpdateConnectorTypeAsync(
        Guid connectorTypeId,
        string nameEn,
        string nameEt,
        bool isActive,
        CancellationToken ct = default)
    {
        var connector = await _chargingRepository.UpdateConnectorTypeAsync(connectorTypeId, nameEn, nameEt, isActive, ct);
        return connector is { } ? ChargingContractMapper.ToContract(connector) : null;
    }

    public Task<bool> DeleteConnectorTypeAsync(Guid connectorTypeId, CancellationToken ct = default)
    {
        return _chargingRepository.DeleteConnectorTypeAsync(connectorTypeId, ct);
    }

    public async Task<ConnectorTypeContract?> SetConnectorTypeActivationAsync(
        Guid connectorTypeId,
        bool isActive,
        CancellationToken ct = default)
    {
        var connector = await _chargingRepository.SetConnectorTypeActivationAsync(connectorTypeId, isActive, ct);
        return connector is { } ? ChargingContractMapper.ToContract(connector) : null;
    }

    public async Task<IReadOnlyCollection<ReservationContract>> GetOverlappingReservationsAsync(
        Guid stationId,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        Guid? excludeReservationId = null,
        CancellationToken ct = default)
    {
        var reservations = await _chargingRepository.GetOverlappingReservationsAsync(
            stationId,
            startTimeUtc,
            endTimeUtc,
            excludeReservationId,
            ct);

        return reservations
            .Select(ChargingContractMapper.ToContract)
            .ToList();
    }

    public async Task<IReadOnlyCollection<ReservationContract>> GetStationReservationsAsync(
        Guid stationId,
        CancellationToken ct = default)
    {
        var reservations = await _chargingRepository.GetStationReservationsAsync(stationId, ct);
        return reservations
            .Select(ChargingContractMapper.ToContract)
            .ToList();
    }

    public async Task<IReadOnlyCollection<ReservationContract>> GetUserReservationsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var reservations = await _chargingRepository.GetUserReservationsAsync(userId, ct);
        return reservations
            .Select(ChargingContractMapper.ToContract)
            .ToList();
    }

    public async Task<ReservationContract?> GetReservationByIdForUserAsync(
        Guid reservationId,
        Guid userId,
        CancellationToken ct = default)
    {
        var reservation = await _chargingRepository.GetReservationByIdForUserAsync(reservationId, userId, ct);
        return reservation is { } ? ChargingContractMapper.ToContract(reservation) : null;
    }

    public async Task<ReservationContract?> GetReservationByIdAsync(Guid reservationId, CancellationToken ct = default)
    {
        var reservation = await _chargingRepository.GetReservationByIdAsync(reservationId, ct);
        return reservation is { } ? ChargingContractMapper.ToContract(reservation) : null;
    }

    public async Task<ReservationContract> CreateReservationAsync(
        ReservationContract reservation,
        CancellationToken ct = default)
    {
        var dto = ChargingContractMapper.ToDto(reservation);
        var created = await _chargingRepository.CreateReservationAsync(dto, ct);
        return ChargingContractMapper.ToContract(created);
    }

    public Task<bool> UpdateReservationStatusAsync(
        Guid reservationId,
        EReservationStatus status,
        DateTime? expiresAtUtc = null,
        DateTime? cancelledAtUtc = null,
        EStationStatus? stationStatus = null,
        CancellationToken ct = default)
    {
        return _chargingRepository.UpdateReservationStatusAsync(
            reservationId,
            status,
            expiresAtUtc,
            cancelledAtUtc,
            stationStatus,
            ct);
    }

    public async Task<ChargingSessionContract?> GetChargingSessionByIdForUserAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken ct = default)
    {
        var session = await _chargingRepository.GetChargingSessionByIdForUserAsync(sessionId, userId, ct);
        return session is { } ? ChargingContractMapper.ToContract(session) : null;
    }

    public async Task<ChargingSessionContract?> GetChargingSessionByIdAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        var session = await _chargingRepository.GetChargingSessionByIdAsync(sessionId, ct);
        return session is { } ? ChargingContractMapper.ToContract(session) : null;
    }

    public async Task<IReadOnlyCollection<ChargingSessionContract>> GetUserChargingSessionsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var sessions = await _chargingRepository.GetUserChargingSessionsAsync(userId, ct);
        return sessions
            .Select(ChargingContractMapper.ToContract)
            .ToList();
    }

    public async Task<ChargingSessionContract?> GetChargingSessionByReservationIdAsync(
        Guid reservationId,
        CancellationToken ct = default)
    {
        var session = await _chargingRepository.GetChargingSessionByReservationIdAsync(reservationId, ct);
        return session is { } ? ChargingContractMapper.ToContract(session) : null;
    }

    public async Task<ChargingSessionContract> CreateChargingSessionAsync(
        ChargingSessionContract session,
        CancellationToken ct = default)
    {
        var dto = ChargingContractMapper.ToDto(session);
        var created = await _chargingRepository.CreateChargingSessionAsync(dto, ct);
        return ChargingContractMapper.ToContract(created);
    }

    public Task<bool> CompleteChargingSessionAsync(
        Guid sessionId,
        DateTime endTimeUtc,
        decimal energyConsumed,
        decimal cost,
        Guid? promotionId,
        EStationStatus stationStatus,
        CancellationToken ct = default)
    {
        return _chargingRepository.CompleteChargingSessionAsync(
            sessionId,
            endTimeUtc,
            energyConsumed,
            cost,
            promotionId,
            stationStatus,
            ct);
    }

    public async Task<IReadOnlyCollection<MaintenanceContract>> GetMaintenancesByCompanyAsync(
        Guid companyId,
        bool includeResolved,
        CancellationToken ct = default)
    {
        var maintenances = await _chargingRepository.GetMaintenancesByCompanyAsync(companyId, includeResolved, ct);
        return maintenances
            .Select(ChargingContractMapper.ToContract)
            .ToList();
    }

    public async Task<MaintenanceContract?> GetMaintenanceByIdForCompanyAsync(
        Guid maintenanceId,
        Guid companyId,
        CancellationToken ct = default)
    {
        var maintenance = await _chargingRepository.GetMaintenanceByIdForCompanyAsync(maintenanceId, companyId, ct);
        return maintenance is { } ? ChargingContractMapper.ToContract(maintenance) : null;
    }

    public async Task<MaintenanceContract> CreateMaintenanceAsync(
        MaintenanceContract maintenance,
        CancellationToken ct = default,
        string? actorUserName = null)
    {
        var dto = ChargingContractMapper.ToDto(maintenance);
        var created = await _chargingRepository.CreateMaintenanceAsync(dto, ct);
        return ChargingContractMapper.ToContract(created);
    }

    public async Task<bool> UpdateMaintenanceStatusAsync(
        Guid maintenanceId,
        EMaintenanceStatus status,
        string? notes,
        DateTime? resolvedAtUtc,
        CancellationToken ct = default,
        string? actorUserName = null)
    {
        return await _chargingRepository.UpdateMaintenanceStatusAsync(maintenanceId, status, notes, resolvedAtUtc, ct);
    }

    public async Task<bool> AssignMaintenanceAsync(
        Guid maintenanceId,
        Guid? assignedToUserId,
        CancellationToken ct = default,
        string? actorUserName = null)
    {
        return await _chargingRepository.AssignMaintenanceAsync(maintenanceId, assignedToUserId, ct);
    }

    public Task<bool> UpdateStationStatusAsync(
        Guid stationId,
        EStationStatus status,
        CancellationToken ct = default)
    {
        return _chargingRepository.UpdateStationStatusAsync(stationId, status, ct);
    }
}
