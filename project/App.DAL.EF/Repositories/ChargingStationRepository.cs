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

    public Task<ChargingStation?> GetByIdWithDetailsAsync(Guid id)
    {
        return _context.ChargingStations
            .Include(station => station.ChargingStationConnectors!)
            .ThenInclude(link => link.Connector!)
            .Include(station => station.Reservations!)
            .FirstOrDefaultAsync(station => station.Id == id && station.IsActive);
    }

    public Task<List<ChargingStation>> GetByCompanyAsync(Guid companyId)
    {
        return _context.ChargingStations
            .Where(station => station.CompanyId == companyId)
            .Include(station => station.ChargingStationConnectors!)
            .ThenInclude(link => link.Connector!)
            .Include(station => station.MaintenanceIssues!)
            .Include(station => station.Reservations!)
            .Include(station => station.ChargingSessions!)
            .OrderBy(station => station.Location)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<ChargingStation?> GetByIdForCompanyAsync(Guid id, Guid companyId)
    {
        return _context.ChargingStations
            .Where(station => station.Id == id && station.CompanyId == companyId)
            .Include(station => station.ChargingStationConnectors!)
            .ThenInclude(link => link.Connector!)
            .Include(station => station.MaintenanceIssues!)
            .Include(station => station.Reservations!)
            .Include(station => station.ChargingSessions!)
            .FirstOrDefaultAsync();
    }
}
