using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal interface IChargingStationConnectorRepository
{
    IQueryable<ChargingStationConnector> Query();
    void Add(ChargingStationConnector entity);
    void RemoveRange(IEnumerable<ChargingStationConnector> entities);
}
