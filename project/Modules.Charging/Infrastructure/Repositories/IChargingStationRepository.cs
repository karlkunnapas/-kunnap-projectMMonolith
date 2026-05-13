using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal interface IChargingStationRepository
{
    IQueryable<ChargingStation> Query();
    void Add(ChargingStation station);
    void Remove(ChargingStation station);
}
