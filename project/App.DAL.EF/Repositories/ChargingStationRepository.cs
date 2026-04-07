using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories;

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

    public IQueryable<ChargingStation> GetStationsForHome(string? status = null, string? location = null)
    {
        var query = GetStationsWithConnectors()
            .Where(station => station.IsActive);

        if (Enum.TryParse<EStationStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(station => station.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var locationFilter = location.Trim().ToLowerInvariant();
            query = query.Where(station => station.Location.ToLower().Contains(locationFilter));
        }

        return query;
    }
}
