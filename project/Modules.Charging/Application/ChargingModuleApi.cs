using Modules.Charging.Application.Services;
using Shared.Contracts.Charging;

namespace Modules.Charging.Application;

internal sealed class ChargingModuleApi : IChargingModuleApi
{
    private readonly IChargingApplicationService _service;

    public ChargingModuleApi(IChargingApplicationService service)
    {
        _service = service;
    }

    public Task<IReadOnlyCollection<ChargingStationContract>> GetStationsForHomeAsync(EStationStatus? status = null, CancellationToken ct = default) => _service.GetStationsForHomeAsync(status, ct);
    public Task<IReadOnlyCollection<AdminChargingStationContract>> GetStationsForAdminAsync(CancellationToken ct = default) => _service.GetStationsForAdminAsync(ct);
    public Task<ChargingStationContract?> GetStationByIdAsync(Guid stationId, CancellationToken ct = default) => _service.GetStationByIdAsync(stationId, ct);
    public Task<Guid?> GetStationCompanyIdAsync(Guid stationId, CancellationToken ct = default) => _service.GetStationCompanyIdAsync(stationId, ct);
    public Task<IReadOnlyCollection<Guid>> GetStationConnectorIdsAsync(Guid stationId, CancellationToken ct = default) => _service.GetStationConnectorIdsAsync(stationId, ct);
    public Task<IReadOnlyCollection<ChargingStationContract>> GetCompanyStationsAsync(Guid companyId, CancellationToken ct = default) => _service.GetCompanyStationsAsync(companyId, ct);
    public Task<ChargingStationContract?> GetCompanyStationByIdAsync(Guid stationId, Guid companyId, CancellationToken ct = default) => _service.GetCompanyStationByIdAsync(stationId, companyId, ct);
    public Task<CompanyDashboardStatsContract> GetCompanyDashboardStatsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => _service.GetCompanyDashboardStatsAsync(companyId, fromUtc, toUtc, ct);
    public Task<IReadOnlyCollection<ChargingSessionContract>> GetCompanyChargingSessionsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => _service.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc, ct);
    public Task<IReadOnlyCollection<ReservationContract>> GetCompanyReservationsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => _service.GetCompanyReservationsAsync(companyId, fromUtc, toUtc, ct);
    public Task<int> GetReservationCountByRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => _service.GetReservationCountByRangeAsync(fromUtc, toUtc, ct);
    public Task<ChargingStationContract> CreateCompanyStationAsync(UpsertCompanyStationContract request, CancellationToken ct = default) => _service.CreateCompanyStationAsync(request, ct);
    public Task<ChargingStationContract?> UpdateCompanyStationAsync(UpsertCompanyStationContract request, CancellationToken ct = default) => _service.UpdateCompanyStationAsync(request, ct);
    public Task<bool> DeleteCompanyStationAsync(Guid stationId, Guid companyId, CancellationToken ct = default) => _service.DeleteCompanyStationAsync(stationId, companyId, ct);
    public Task<IReadOnlyCollection<Guid>> GetStationAssignedConnectorIdsAsync(Guid stationId, CancellationToken ct = default) => _service.GetStationAssignedConnectorIdsAsync(stationId, ct);
    public Task SetStationConnectorsAsync(Guid stationId, IReadOnlyCollection<Guid> connectorIds, CancellationToken ct = default) => _service.SetStationConnectorsAsync(stationId, connectorIds, ct);
    public Task<IReadOnlyCollection<ConnectorContract>> GetConnectorsAsync(bool includeInactive = false, CancellationToken ct = default) => _service.GetConnectorsAsync(includeInactive, ct);
    public Task<ConnectorTypeContract?> GetConnectorTypeByIdAsync(Guid connectorTypeId, CancellationToken ct = default) => _service.GetConnectorTypeByIdAsync(connectorTypeId, ct);
    public Task<ConnectorTypeContract> CreateConnectorTypeAsync(string nameEn, string nameEt, bool isActive, CancellationToken ct = default) => _service.CreateConnectorTypeAsync(nameEn, nameEt, isActive, ct);
    public Task<ConnectorTypeContract?> UpdateConnectorTypeAsync(Guid connectorTypeId, string nameEn, string nameEt, bool isActive, CancellationToken ct = default) => _service.UpdateConnectorTypeAsync(connectorTypeId, nameEn, nameEt, isActive, ct);
    public Task<bool> DeleteConnectorTypeAsync(Guid connectorTypeId, CancellationToken ct = default) => _service.DeleteConnectorTypeAsync(connectorTypeId, ct);
    public Task<IReadOnlyCollection<ReservationContract>> GetOverlappingReservationsAsync(Guid stationId, DateTime startTimeUtc, DateTime endTimeUtc, Guid? excludeReservationId = null, CancellationToken ct = default) => _service.GetOverlappingReservationsAsync(stationId, startTimeUtc, endTimeUtc, excludeReservationId, ct);
    public Task<IReadOnlyCollection<ReservationContract>> GetStationReservationsAsync(Guid stationId, CancellationToken ct = default) => _service.GetStationReservationsAsync(stationId, ct);
    public Task<IReadOnlyCollection<ReservationContract>> GetUserReservationsAsync(Guid userId, CancellationToken ct = default) => _service.GetUserReservationsAsync(userId, ct);
    public Task<ReservationContract?> GetReservationByIdForUserAsync(Guid reservationId, Guid userId, CancellationToken ct = default) => _service.GetReservationByIdForUserAsync(reservationId, userId, ct);
    public Task<ReservationContract?> GetReservationByIdAsync(Guid reservationId, CancellationToken ct = default) => _service.GetReservationByIdAsync(reservationId, ct);
    public Task<ReservationContract> CreateReservationAsync(ReservationContract reservation, CancellationToken ct = default) => _service.CreateReservationAsync(reservation, ct);
    public Task<bool> UpdateReservationStatusAsync(Guid reservationId, EReservationStatus status, DateTime? expiresAtUtc = null, DateTime? cancelledAtUtc = null, EStationStatus? stationStatus = null, CancellationToken ct = default) => _service.UpdateReservationStatusAsync(reservationId, status, expiresAtUtc, cancelledAtUtc, stationStatus, ct);
    public Task<ChargingSessionContract?> GetChargingSessionByIdForUserAsync(Guid sessionId, Guid userId, CancellationToken ct = default) => _service.GetChargingSessionByIdForUserAsync(sessionId, userId, ct);
    public Task<ChargingSessionContract?> GetChargingSessionByIdAsync(Guid sessionId, CancellationToken ct = default) => _service.GetChargingSessionByIdAsync(sessionId, ct);
    public Task<IReadOnlyCollection<ChargingSessionContract>> GetUserChargingSessionsAsync(Guid userId, CancellationToken ct = default) => _service.GetUserChargingSessionsAsync(userId, ct);
    public Task<ChargingSessionContract?> GetChargingSessionByReservationIdAsync(Guid reservationId, CancellationToken ct = default) => _service.GetChargingSessionByReservationIdAsync(reservationId, ct);
    public Task<ChargingSessionContract> CreateChargingSessionAsync(ChargingSessionContract session, CancellationToken ct = default) => _service.CreateChargingSessionAsync(session, ct);
    public Task<bool> CompleteChargingSessionAsync(Guid sessionId, DateTime endTimeUtc, decimal energyConsumed, decimal cost, Guid? promotionId, EStationStatus stationStatus, CancellationToken ct = default) => _service.CompleteChargingSessionAsync(sessionId, endTimeUtc, energyConsumed, cost, promotionId, stationStatus, ct);
    public Task<IReadOnlyCollection<MaintenanceContract>> GetMaintenancesByCompanyAsync(Guid companyId, bool includeResolved, CancellationToken ct = default) => _service.GetMaintenancesByCompanyAsync(companyId, includeResolved, ct);
    public Task<MaintenanceContract?> GetMaintenanceByIdForCompanyAsync(Guid maintenanceId, Guid companyId, CancellationToken ct = default) => _service.GetMaintenanceByIdForCompanyAsync(maintenanceId, companyId, ct);
    public Task<MaintenanceContract> CreateMaintenanceAsync(MaintenanceContract maintenance, CancellationToken ct = default) => _service.CreateMaintenanceAsync(maintenance, ct);
    public Task<bool> UpdateMaintenanceStatusAsync(Guid maintenanceId, EMaintenanceStatus status, string? notes, DateTime? resolvedAtUtc, CancellationToken ct = default) => _service.UpdateMaintenanceStatusAsync(maintenanceId, status, notes, resolvedAtUtc, ct);
    public Task<bool> AssignMaintenanceAsync(Guid maintenanceId, Guid? assignedToUserId, CancellationToken ct = default) => _service.AssignMaintenanceAsync(maintenanceId, assignedToUserId, ct);
    public Task<bool> UpdateStationStatusAsync(Guid stationId, EStationStatus status, CancellationToken ct = default) => _service.UpdateStationStatusAsync(stationId, status, ct);
}
