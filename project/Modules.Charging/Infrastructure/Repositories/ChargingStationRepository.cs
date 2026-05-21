using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal sealed class ChargingStationRepository : IChargingStationRepository
{
    private readonly ChargingDbContext _dbContext;

    public ChargingStationRepository(ChargingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<ChargingStation> Query()
    {
        return _dbContext.ChargingStations;
    }

    public void Add(ChargingStation station)
    {
        _dbContext.ChargingStations.Add(station);
    }

    public void Remove(ChargingStation station)
    {
        _dbContext.ChargingStations.Remove(station);
    }
}
