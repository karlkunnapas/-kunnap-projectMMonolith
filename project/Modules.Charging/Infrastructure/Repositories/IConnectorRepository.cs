using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal interface IConnectorRepository
{
    IQueryable<Connector> Query();
    void Add(Connector connector);
    void Remove(Connector connector);
}
