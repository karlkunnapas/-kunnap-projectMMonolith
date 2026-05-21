using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal sealed class ConnectorRepository : IConnectorRepository
{
    private readonly ChargingDbContext _dbContext;

    public ConnectorRepository(ChargingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<Connector> Query()
    {
        return _dbContext.Connectors;
    }

    public void Add(Connector connector)
    {
        _dbContext.Connectors.Add(connector);
    }

    public void Remove(Connector connector)
    {
        _dbContext.Connectors.Remove(connector);
    }
}
