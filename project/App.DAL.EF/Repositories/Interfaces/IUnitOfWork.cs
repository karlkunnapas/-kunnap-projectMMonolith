using App.Domain;

namespace App.DAL.EF.Repositories.Interfaces;

/// <summary>
/// Unit of Work interface that coordinates repository operations and provides SaveAsync.
/// All write operations across repositories should be committed via a single SaveAsync call.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    // Typed repository properties for all domain entities
    ICompanyRepository Companies { get; }

    IChargingStationRepository ChargingStations { get; }
    IChargingSessionRepository ChargingSessions { get; }
    IMaintenanceRepository Maintenances { get; }
    IRepository<ChargingStationConnector> ChargingStationConnectors { get; }
    IRepository<Connector> Connectors { get; }
    IRepository<AppUserCompany> AppUserCompanies { get; }
    IRepository<AuditLog> AuditLogs { get; }
    IAuditLogRepository AuditLogQueries { get; }
    IVehicleRepository Vehicles { get; }
    IVehicleConnectorRepository VehicleConnectors { get; }
    IReservationRepository Reservations { get; }
    IPromotionRepository Promotions { get; }
    IUserPromotionRepository UserPromotions { get; }

    /// <summary>
    /// Commits all tracked changes to the database.
    /// The DbContext automatically handles:
    /// - Soft delete conversion (setting IsDeleted instead of physical delete)
    /// - Audit field stamping (CreatedAtUtc, UpdatedAtUtc, CreatedByUserId, CompanyId)
    /// - Automatic audit log entries for EF-tracked changes
    /// </summary>
    Task<int> SaveAsync();
}
