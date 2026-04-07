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
    private IRepository<ChargingStationConnector>? _chargingStationConnectors;
    private IRepository<Connector>? _connectors;
    private IRepository<AppUserCompany>? _appUserCompanies;
    private IRepository<AuditLog>? _auditLogs;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public ICompanyRepository Companies => _companies ??= new CompanyRepository(_context);
    public IChargingStationRepository ChargingStations =>
        _chargingStations ??= new App.DAL.EF.Repositories.ChargingStationRepository(_context);
    public IRepository<ChargingStationConnector> ChargingStationConnectors =>
        _chargingStationConnectors ??= new Repository<ChargingStationConnector>(_context);
    public IRepository<Connector> Connectors => _connectors ??= new Repository<Connector>(_context);
    public IRepository<AppUserCompany> AppUserCompanies => _appUserCompanies ??= new Repository<AppUserCompany>(_context);
    public IRepository<AuditLog> AuditLogs => _auditLogs ??= new Repository<AuditLog>(_context);

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