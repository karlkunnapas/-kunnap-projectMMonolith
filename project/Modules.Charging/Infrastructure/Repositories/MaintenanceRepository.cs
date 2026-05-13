using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal sealed class MaintenanceRepository : IMaintenanceRepository
{
    private readonly ChargingDbContext _dbContext;

    public MaintenanceRepository(ChargingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<Maintenance> Query() => _dbContext.Maintenances;

    public void Add(Maintenance maintenance) => _dbContext.Maintenances.Add(maintenance);
}
