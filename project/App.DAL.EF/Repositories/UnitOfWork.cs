using App.DAL.EF.Repositories.Interfaces;
using App.Domain;

namespace App.DAL.EF.Repositories.Implementations;

/// <summary>
/// Unit of Work implementation that coordinates all repository operations.
/// Wraps a single ApplicationDbContext instance to ensure transactional consistency.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    // Lazy-initialized repositories
    private ICompanyRepository? _companies;

    private IChargingStationRepository? _chargingStations;
    private IChargingSessionRepository? _chargingSessions;
    private IMaintenanceRepository? _maintenances;
    private IRepository<ChargingStationConnector>? _chargingStationConnectors;
    private IRepository<Connector>? _connectors;
    private IRepository<AppUserCompany>? _appUserCompanies;
    private IRepository<AuditLog>? _auditLogs;
    private IAuditLogRepository? _auditLogQueries;
    private IVehicleRepository? _vehicles;
    private IVehicleConnectorRepository? _vehicleConnectors;
    private IReservationRepository? _reservations;
    private IPromotionRepository? _promotions;
    private IUserPromotionRepository? _userPromotions;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public ICompanyRepository Companies => _companies ??= new CompanyRepository(_context);
    public IChargingStationRepository ChargingStations =>
        _chargingStations ??= new App.DAL.EF.Repositories.ChargingStationRepository(_context);
    public IChargingSessionRepository ChargingSessions =>
        _chargingSessions ??= new App.DAL.EF.Repositories.ChargingSessionRepository(_context);
    public IMaintenanceRepository Maintenances =>
        _maintenances ??= new App.DAL.EF.Repositories.MaintenanceRepository(_context);
    public IRepository<ChargingStationConnector> ChargingStationConnectors =>
        _chargingStationConnectors ??= new Repository<ChargingStationConnector>(_context);
    public IRepository<Connector> Connectors => _connectors ??= new Repository<Connector>(_context);
    public IRepository<AppUserCompany> AppUserCompanies => _appUserCompanies ??= new Repository<AppUserCompany>(_context);
    public IRepository<AuditLog> AuditLogs => _auditLogs ??= new Repository<AuditLog>(_context);
    public IAuditLogRepository AuditLogQueries => _auditLogQueries ??= new App.DAL.EF.Repositories.AuditLogRepository(_context);
    public IVehicleRepository Vehicles => _vehicles ??= new App.DAL.EF.Repositories.VehicleRepository(_context);
    public IVehicleConnectorRepository VehicleConnectors => _vehicleConnectors ??= new App.DAL.EF.Repositories.VehicleConnectorRepository(_context);
    public IReservationRepository Reservations => _reservations ??= new App.DAL.EF.Repositories.ReservationRepository(_context);
    public IPromotionRepository Promotions => _promotions ??= new App.DAL.EF.Repositories.PromotionRepository(_context);
    public IUserPromotionRepository UserPromotions => _userPromotions ??= new App.DAL.EF.Repositories.UserPromotionRepository(_context);

    /// <summary>
    /// Commits all tracked changes to the database.
    /// The DbContext automatically handles soft delete conversion, audit stamping, and audit logging.
    /// </summary>
    public async Task<int> SaveAsync()
    {
        return await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Disposes the underlying DbContext.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
