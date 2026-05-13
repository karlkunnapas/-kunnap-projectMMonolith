using Modules.Charging.Application.Mappers;
using Modules.Charging.Infrastructure;
using Shared.Contracts.Charging;

namespace Modules.Charging.Application.Services;

internal sealed class ChargingApplicationService : IChargingApplicationService
{
    private readonly IChargingRepository _chargingRepository;

    public ChargingApplicationService(IChargingRepository chargingRepository)
    {
        _chargingRepository = chargingRepository;
    }

    public async Task<IReadOnlyCollection<ChargingStationContract>> GetStationsForHomeAsync(EStationStatus? status = null, CancellationToken ct = default)
        => (await _chargingRepository.GetStationsForHomeAsync(status, ct)).Select(ChargingContractMapper.ToContract).ToList();

    public async Task<IReadOnlyCollection<AdminChargingStationContract>> GetStationsForAdminAsync(CancellationToken ct = default)
        => (await _chargingRepository.GetStationsForAdminAsync(ct)).Select(ChargingContractMapper.ToContract).ToList();

    public async Task<ChargingStationContract?> GetStationByIdAsync(Guid stationId, CancellationToken ct = default)
        => (await _chargingRepository.GetStationByIdAsync(stationId, ct)) is { } dto ? ChargingContractMapper.ToContract(dto) : null;

    public Task<Guid?> GetStationCompanyIdAsync(Guid stationId, CancellationToken ct = default) => _chargingRepository.GetStationCompanyIdAsync(stationId, ct);

    public Task<IReadOnlyCollection<Guid>> GetStationConnectorIdsAsync(Guid stationId, CancellationToken ct = default) => _chargingRepository.GetStationConnectorIdsAsync(stationId, ct);

    public async Task<IReadOnlyCollection<ChargingStationContract>> GetCompanyStationsAsync(Guid companyId, CancellationToken ct = default)
        => (await _chargingRepository.GetCompanyStationsAsync(companyId, ct)).Select(ChargingContractMapper.ToContract).ToList();

    public async Task<ChargingStationContract?> GetCompanyStationByIdAsync(Guid stationId, Guid companyId, CancellationToken ct = default)
        => (await _chargingRepository.GetCompanyStationByIdAsync(stationId, companyId, ct)) is { } dto ? ChargingContractMapper.ToContract(dto) : null;

    public async Task<CompanyDashboardStatsContract> GetCompanyDashboardStatsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        => ChargingContractMapper.ToContract(await _chargingRepository.GetCompanyDashboardStatsAsync(companyId, fromUtc, toUtc, ct));

    public async Task<IReadOnlyCollection<ChargingSessionContract>> GetCompanyChargingSessionsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        => (await _chargingRepository.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc, ct)).Select(ChargingContractMapper.ToContract).ToList();

    public async Task<IReadOnlyCollection<ReservationContract>> GetCompanyReservationsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        => (await _chargingRepository.GetCompanyReservationsAsync(companyId, fromUtc, toUtc, ct)).Select(ChargingContractMapper.ToContract).ToList();

    public Task<int> GetReservationCountByRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => _chargingRepository.GetReservationCountByRangeAsync(fromUtc, toUtc, ct);

    public async Task<ChargingStationContract> CreateCompanyStationAsync(UpsertCompanyStationContract request, CancellationToken ct = default)
        => ChargingContractMapper.ToContract(await _chargingRepository.CreateCompanyStationAsync(ChargingContractMapper.ToDto(request), ct));

    public async Task<ChargingStationContract?> UpdateCompanyStationAsync(UpsertCompanyStationContract request, CancellationToken ct = default)
        => (await _chargingRepository.UpdateCompanyStationAsync(ChargingContractMapper.ToDto(request), ct)) is { } dto ? ChargingContractMapper.ToContract(dto) : null;

    public Task<bool> DeleteCompanyStationAsync(Guid stationId, Guid companyId, CancellationToken ct = default) => _chargingRepository.DeleteCompanyStationAsync(stationId, companyId, ct);

    public Task<IReadOnlyCollection<Guid>> GetStationAssignedConnectorIdsAsync(Guid stationId, CancellationToken ct = default) => _chargingRepository.GetStationAssignedConnectorIdsAsync(stationId, ct);

    public Task SetStationConnectorsAsync(Guid stationId, IReadOnlyCollection<Guid> connectorIds, CancellationToken ct = default) => _chargingRepository.SetStationConnectorsAsync(stationId, connectorIds, ct);

    public async Task<IReadOnlyCollection<ConnectorContract>> GetConnectorsAsync(bool includeInactive = false, CancellationToken ct = default)
        => (await _chargingRepository.GetConnectorsAsync(includeInactive, ct)).Select(ChargingContractMapper.ToContract).ToList();

    public async Task<ConnectorTypeContract?> GetConnectorTypeByIdAsync(Guid connectorTypeId, CancellationToken ct = default)
        => (await _chargingRepository.GetConnectorTypeByIdAsync(connectorTypeId, ct)) is { } dto ? ChargingContractMapper.ToContract(dto) : null;

    public async Task<ConnectorTypeContract> CreateConnectorTypeAsync(string nameEn, string nameEt, bool isActive, CancellationToken ct = default)
        => ChargingContractMapper.ToContract(await _chargingRepository.CreateConnectorTypeAsync(nameEn, nameEt, isActive, ct));

    public async Task<ConnectorTypeContract?> UpdateConnectorTypeAsync(Guid connectorTypeId, string nameEn, string nameEt, bool isActive, CancellationToken ct = default)
        => (await _chargingRepository.UpdateConnectorTypeAsync(connectorTypeId, nameEn, nameEt, isActive, ct)) is { } dto ? ChargingContractMapper.ToContract(dto) : null;

    public Task<bool> DeleteConnectorTypeAsync(Guid connectorTypeId, CancellationToken ct = default) => _chargingRepository.DeleteConnectorTypeAsync(connectorTypeId, ct);

    public async Task<IReadOnlyCollection<ReservationContract>> GetOverlappingReservationsAsync(Guid stationId, DateTime startTimeUtc, DateTime endTimeUtc, Guid? excludeReservationId = null, CancellationToken ct = default)
        => (await _chargingRepository.GetOverlappingReservationsAsync(stationId, startTimeUtc, endTimeUtc, excludeReservationId, ct)).Select(ChargingContractMapper.ToContract).ToList();

    public async Task<IReadOnlyCollection<ReservationContract>> GetStationReservationsAsync(Guid stationId, CancellationToken ct = default)
        => (await _chargingRepository.GetStationReservationsAsync(stationId, ct)).Select(ChargingContractMapper.ToContract).ToList();

    public async Task<IReadOnlyCollection<ReservationContract>> GetUserReservationsAsync(Guid userId, CancellationToken ct = default)
        => (await _chargingRepository.GetUserReservationsAsync(userId, ct)).Select(ChargingContractMapper.ToContract).ToList();

    public async Task<ReservationContract?> GetReservationByIdForUserAsync(Guid reservationId, Guid userId, CancellationToken ct = default)
        => (await _chargingRepository.GetReservationByIdForUserAsync(reservationId, userId, ct)) is { } dto ? ChargingContractMapper.ToContract(dto) : null;

    public async Task<ReservationContract?> GetReservationByIdAsync(Guid reservationId, CancellationToken ct = default)
        => (await _chargingRepository.GetReservationByIdAsync(reservationId, ct)) is { } dto ? ChargingContractMapper.ToContract(dto) : null;

    public async Task<ReservationContract> CreateReservationAsync(ReservationContract reservation, CancellationToken ct = default)
        => ChargingContractMapper.ToContract(await _chargingRepository.CreateReservationAsync(ChargingContractMapper.ToDto(reservation), ct));

    public Task<bool> UpdateReservationStatusAsync(Guid reservationId, EReservationStatus status, DateTime? expiresAtUtc = null, DateTime? cancelledAtUtc = null, EStationStatus? stationStatus = null, CancellationToken ct = default)
        => _chargingRepository.UpdateReservationStatusAsync(reservationId, status, expiresAtUtc, cancelledAtUtc, stationStatus, ct);

    public async Task<ChargingSessionContract?> GetChargingSessionByIdForUserAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
        => (await _chargingRepository.GetChargingSessionByIdForUserAsync(sessionId, userId, ct)) is { } dto ? ChargingContractMapper.ToContract(dto) : null;

    public async Task<ChargingSessionContract?> GetChargingSessionByIdAsync(Guid sessionId, CancellationToken ct = default)
        => (await _chargingRepository.GetChargingSessionByIdAsync(sessionId, ct)) is { } dto ? ChargingContractMapper.ToContract(dto) : null;

    public async Task<IReadOnlyCollection<ChargingSessionContract>> GetUserChargingSessionsAsync(Guid userId, CancellationToken ct = default)
        => (await _chargingRepository.GetUserChargingSessionsAsync(userId, ct)).Select(ChargingContractMapper.ToContract).ToList();

    public async Task<ChargingSessionContract?> GetChargingSessionByReservationIdAsync(Guid reservationId, CancellationToken ct = default)
        => (await _chargingRepository.GetChargingSessionByReservationIdAsync(reservationId, ct)) is { } dto ? ChargingContractMapper.ToContract(dto) : null;

    public async Task<ChargingSessionContract> CreateChargingSessionAsync(ChargingSessionContract session, CancellationToken ct = default)
        => ChargingContractMapper.ToContract(await _chargingRepository.CreateChargingSessionAsync(ChargingContractMapper.ToDto(session), ct));

    public Task<bool> CompleteChargingSessionAsync(Guid sessionId, DateTime endTimeUtc, decimal energyConsumed, decimal cost, Guid? promotionId, EStationStatus stationStatus, CancellationToken ct = default)
        => _chargingRepository.CompleteChargingSessionAsync(sessionId, endTimeUtc, energyConsumed, cost, promotionId, stationStatus, ct);

    public async Task<IReadOnlyCollection<MaintenanceContract>> GetMaintenancesByCompanyAsync(Guid companyId, bool includeResolved, CancellationToken ct = default)
        => (await _chargingRepository.GetMaintenancesByCompanyAsync(companyId, includeResolved, ct)).Select(ChargingContractMapper.ToContract).ToList();

    public async Task<MaintenanceContract?> GetMaintenanceByIdForCompanyAsync(Guid maintenanceId, Guid companyId, CancellationToken ct = default)
        => (await _chargingRepository.GetMaintenanceByIdForCompanyAsync(maintenanceId, companyId, ct)) is { } dto ? ChargingContractMapper.ToContract(dto) : null;

    public async Task<MaintenanceContract> CreateMaintenanceAsync(MaintenanceContract maintenance, CancellationToken ct = default)
        => ChargingContractMapper.ToContract(await _chargingRepository.CreateMaintenanceAsync(ChargingContractMapper.ToDto(maintenance), ct));

    public Task<bool> UpdateMaintenanceStatusAsync(Guid maintenanceId, EMaintenanceStatus status, string? notes, DateTime? resolvedAtUtc, CancellationToken ct = default)
        => _chargingRepository.UpdateMaintenanceStatusAsync(maintenanceId, status, notes, resolvedAtUtc, ct);

    public Task<bool> AssignMaintenanceAsync(Guid maintenanceId, Guid? assignedToUserId, CancellationToken ct = default)
        => _chargingRepository.AssignMaintenanceAsync(maintenanceId, assignedToUserId, ct);

    public Task<bool> UpdateStationStatusAsync(Guid stationId, EStationStatus status, CancellationToken ct = default)
        => _chargingRepository.UpdateStationStatusAsync(stationId, status, ct);
}
