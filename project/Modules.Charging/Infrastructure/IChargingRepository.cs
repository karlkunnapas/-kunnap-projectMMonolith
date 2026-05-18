using Modules.Charging.Application.DTO;
using Shared.Contracts.Charging;

namespace Modules.Charging.Infrastructure;

internal interface IChargingRepository
{
    Task<IReadOnlyCollection<ChargingStationDto>> GetStationsForHomeAsync(EStationStatus? status = null, CancellationToken ct = default);
    Task<IReadOnlyCollection<AdminChargingStationDto>> GetStationsForAdminAsync(CancellationToken ct = default);
    Task<ChargingStationDto?> GetStationByIdAsync(Guid stationId, CancellationToken ct = default);
    Task<Guid?> GetStationCompanyIdAsync(Guid stationId, CancellationToken ct = default);
    Task<IReadOnlyCollection<Guid>> GetStationConnectorIdsAsync(Guid stationId, CancellationToken ct = default);
    Task<IReadOnlyCollection<ChargingStationDto>> GetCompanyStationsAsync(Guid companyId, CancellationToken ct = default);
    Task<ChargingStationDto?> GetCompanyStationByIdAsync(Guid stationId, Guid companyId, CancellationToken ct = default);
    Task<CompanyDashboardStatsDto> GetCompanyDashboardStatsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyCollection<ChargingSessionDto>> GetCompanyChargingSessionsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyCollection<ReservationDto>> GetCompanyReservationsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<int> GetReservationCountByRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<ChargingStationDto> CreateCompanyStationAsync(UpsertCompanyStationDto request, CancellationToken ct = default);
    Task<ChargingStationDto?> UpdateCompanyStationAsync(UpsertCompanyStationDto request, CancellationToken ct = default);
    Task<bool> DeleteCompanyStationAsync(Guid stationId, Guid companyId, CancellationToken ct = default);
    Task<ChargingStationDto?> SetCompanyStationActivationAsync(Guid stationId, Guid companyId, bool isActive, CancellationToken ct = default);
    Task<IReadOnlyCollection<Guid>> GetStationAssignedConnectorIdsAsync(Guid stationId, CancellationToken ct = default);
    Task SetStationConnectorsAsync(Guid stationId, IReadOnlyCollection<Guid> connectorIds, CancellationToken ct = default);
    Task<IReadOnlyCollection<ConnectorDto>> GetConnectorsAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<ConnectorTypeDto?> GetConnectorTypeByIdAsync(Guid connectorTypeId, CancellationToken ct = default);
    Task<ConnectorTypeDto> CreateConnectorTypeAsync(string nameEn, string nameEt, bool isActive, CancellationToken ct = default);
    Task<ConnectorTypeDto?> UpdateConnectorTypeAsync(Guid connectorTypeId, string nameEn, string nameEt, bool isActive, CancellationToken ct = default);
    Task<bool> DeleteConnectorTypeAsync(Guid connectorTypeId, CancellationToken ct = default);
    Task<ConnectorTypeDto?> SetConnectorTypeActivationAsync(Guid connectorTypeId, bool isActive, CancellationToken ct = default);
    Task<IReadOnlyCollection<ReservationDto>> GetOverlappingReservationsAsync(Guid stationId, DateTime startTimeUtc, DateTime endTimeUtc, Guid? excludeReservationId = null, CancellationToken ct = default);
    Task<IReadOnlyCollection<ReservationDto>> GetStationReservationsAsync(Guid stationId, CancellationToken ct = default);
    Task<IReadOnlyCollection<ReservationDto>> GetUserReservationsAsync(Guid userId, CancellationToken ct = default);
    Task<ReservationDto?> GetReservationByIdForUserAsync(Guid reservationId, Guid userId, CancellationToken ct = default);
    Task<ReservationDto?> GetReservationByIdAsync(Guid reservationId, CancellationToken ct = default);
    Task<ReservationDto> CreateReservationAsync(ReservationDto reservation, CancellationToken ct = default);
    Task<bool> UpdateReservationStatusAsync(Guid reservationId, EReservationStatus status, DateTime? expiresAtUtc = null, DateTime? cancelledAtUtc = null, EStationStatus? stationStatus = null, CancellationToken ct = default);
    Task<ChargingSessionDto?> GetChargingSessionByIdForUserAsync(Guid sessionId, Guid userId, CancellationToken ct = default);
    Task<ChargingSessionDto?> GetChargingSessionByIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyCollection<ChargingSessionDto>> GetUserChargingSessionsAsync(Guid userId, CancellationToken ct = default);
    Task<ChargingSessionDto?> GetChargingSessionByReservationIdAsync(Guid reservationId, CancellationToken ct = default);
    Task<ChargingSessionDto> CreateChargingSessionAsync(ChargingSessionDto session, CancellationToken ct = default);
    Task<bool> CompleteChargingSessionAsync(Guid sessionId, DateTime endTimeUtc, decimal energyConsumed, decimal cost, Guid? promotionId, EStationStatus stationStatus, CancellationToken ct = default);
    Task<IReadOnlyCollection<MaintenanceDto>> GetMaintenancesByCompanyAsync(Guid companyId, bool includeResolved, CancellationToken ct = default);
    Task<MaintenanceDto?> GetMaintenanceByIdAsync(Guid maintenanceId, CancellationToken ct = default);
    Task<MaintenanceDto?> GetMaintenanceByIdForCompanyAsync(Guid maintenanceId, Guid companyId, CancellationToken ct = default);
    Task<MaintenanceDto> CreateMaintenanceAsync(MaintenanceDto maintenance, CancellationToken ct = default);
    Task<bool> UpdateMaintenanceStatusAsync(Guid maintenanceId, EMaintenanceStatus status, string? notes, DateTime? resolvedAtUtc, CancellationToken ct = default);
    Task<bool> AssignMaintenanceAsync(Guid maintenanceId, Guid? assignedToUserId, CancellationToken ct = default);
    Task<bool> UpdateStationStatusAsync(Guid stationId, EStationStatus status, CancellationToken ct = default);
}
