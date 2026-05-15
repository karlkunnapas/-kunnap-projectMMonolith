namespace Shared.Contracts.Charging;

public interface IChargingModuleApi
{
    Task<IReadOnlyCollection<ChargingStationContract>> GetStationsForHomeAsync(EStationStatus? status = null, CancellationToken ct = default);
    Task<IReadOnlyCollection<AdminChargingStationContract>> GetStationsForAdminAsync(CancellationToken ct = default);
    Task<ChargingStationContract?> GetStationByIdAsync(Guid stationId, CancellationToken ct = default);
    Task<Guid?> GetStationCompanyIdAsync(Guid stationId, CancellationToken ct = default);
    Task<IReadOnlyCollection<Guid>> GetStationConnectorIdsAsync(Guid stationId, CancellationToken ct = default);

    Task<IReadOnlyCollection<ChargingStationContract>> GetCompanyStationsAsync(Guid companyId, CancellationToken ct = default);
    Task<ChargingStationContract?> GetCompanyStationByIdAsync(Guid stationId, Guid companyId, CancellationToken ct = default);
    Task<CompanyDashboardStatsContract> GetCompanyDashboardStatsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyCollection<ChargingSessionContract>> GetCompanyChargingSessionsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyCollection<ReservationContract>> GetCompanyReservationsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<int> GetReservationCountByRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<ChargingStationContract> CreateCompanyStationAsync(UpsertCompanyStationContract request, CancellationToken ct = default);
    Task<ChargingStationContract?> UpdateCompanyStationAsync(UpsertCompanyStationContract request, CancellationToken ct = default);
    Task<bool> DeleteCompanyStationAsync(Guid stationId, Guid companyId, CancellationToken ct = default);
    Task<IReadOnlyCollection<Guid>> GetStationAssignedConnectorIdsAsync(Guid stationId, CancellationToken ct = default);
    Task SetStationConnectorsAsync(Guid stationId, IReadOnlyCollection<Guid> connectorIds, CancellationToken ct = default);

    Task<IReadOnlyCollection<ConnectorContract>> GetConnectorsAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<ConnectorTypeContract?> GetConnectorTypeByIdAsync(Guid connectorTypeId, CancellationToken ct = default);
    Task<ConnectorTypeContract> CreateConnectorTypeAsync(string nameEn, string nameEt, bool isActive, CancellationToken ct = default);
    Task<ConnectorTypeContract?> UpdateConnectorTypeAsync(Guid connectorTypeId, string nameEn, string nameEt, bool isActive, CancellationToken ct = default);
    Task<bool> DeleteConnectorTypeAsync(Guid connectorTypeId, CancellationToken ct = default);
    Task<IReadOnlyCollection<ReservationContract>> GetOverlappingReservationsAsync(
        Guid stationId,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        Guid? excludeReservationId = null,
        CancellationToken ct = default);

    Task<IReadOnlyCollection<ReservationContract>> GetStationReservationsAsync(Guid stationId, CancellationToken ct = default);
    Task<IReadOnlyCollection<ReservationContract>> GetUserReservationsAsync(Guid userId, CancellationToken ct = default);
    Task<ReservationContract?> GetReservationByIdForUserAsync(Guid reservationId, Guid userId, CancellationToken ct = default);
    Task<ReservationContract?> GetReservationByIdAsync(Guid reservationId, CancellationToken ct = default);
    Task<ReservationContract> CreateReservationAsync(ReservationContract reservation, CancellationToken ct = default);
    Task<bool> UpdateReservationStatusAsync(
        Guid reservationId,
        EReservationStatus status,
        DateTime? expiresAtUtc = null,
        DateTime? cancelledAtUtc = null,
        EStationStatus? stationStatus = null,
        CancellationToken ct = default);

    Task<ChargingSessionContract?> GetChargingSessionByIdForUserAsync(Guid sessionId, Guid userId, CancellationToken ct = default);
    Task<ChargingSessionContract?> GetChargingSessionByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyCollection<ChargingSessionContract>> GetUserChargingSessionsAsync(Guid userId, CancellationToken ct = default);
    Task<ChargingSessionContract?> GetChargingSessionByReservationIdAsync(Guid reservationId, CancellationToken ct = default);
    Task<ChargingSessionContract> CreateChargingSessionAsync(ChargingSessionContract session, CancellationToken ct = default);
    Task<bool> CompleteChargingSessionAsync(
        Guid sessionId,
        DateTime endTimeUtc,
        decimal energyConsumed,
        decimal cost,
        Guid? promotionId,
        EStationStatus stationStatus,
        CancellationToken ct = default);

    Task<IReadOnlyCollection<MaintenanceContract>> GetMaintenancesByCompanyAsync(Guid companyId, bool includeResolved, CancellationToken ct = default);
    Task<MaintenanceContract?> GetMaintenanceByIdForCompanyAsync(Guid maintenanceId, Guid companyId, CancellationToken ct = default);
    Task<MaintenanceContract> CreateMaintenanceAsync(MaintenanceContract maintenance, CancellationToken ct = default, string? actorUserName = null);
    Task<bool> UpdateMaintenanceStatusAsync(Guid maintenanceId, EMaintenanceStatus status, string? notes, DateTime? resolvedAtUtc, CancellationToken ct = default, string? actorUserName = null);
    Task<bool> AssignMaintenanceAsync(Guid maintenanceId, Guid? assignedToUserId, CancellationToken ct = default, string? actorUserName = null);
    Task<bool> UpdateStationStatusAsync(Guid stationId, EStationStatus status, CancellationToken ct = default);
}
