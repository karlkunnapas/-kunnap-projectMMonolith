using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories.Implementations;

public class ChargingStationRepository : IChargingStationRepository
{
    private readonly AppDbContext _context;

    public ChargingStationRepository(AppDbContext context)
    {
        _context = context;
    }

    public IQueryable<ChargingStation> GetStationsWithConnectors()
    {
        return _context.ChargingStations
            .Include(station => station.ChargingStationConnectors!)
            .ThenInclude(link => link.Connector!)
            .AsQueryable();
    }
}
