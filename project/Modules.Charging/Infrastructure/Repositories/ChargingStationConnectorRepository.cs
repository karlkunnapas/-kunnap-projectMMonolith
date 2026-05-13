using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal sealed class ChargingStationConnectorRepository : IChargingStationConnectorRepository
{
    private readonly ChargingDbContext _dbContext;

    public ChargingStationConnectorRepository(ChargingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<ChargingStationConnector> Query() => _dbContext.ChargingStationConnectors;

    public void Add(ChargingStationConnector entity) => _dbContext.ChargingStationConnectors.Add(entity);

    public void RemoveRange(IEnumerable<ChargingStationConnector> entities) => _dbContext.ChargingStationConnectors.RemoveRange(entities);
}
