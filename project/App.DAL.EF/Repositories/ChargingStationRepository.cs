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
            .Where(station => station.IsActive
                              && (!station.CompanyId.HasValue
                                  || _context.Companies.Any(company => company.Id == station.CompanyId && company.IsActive)));

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
            .FirstOrDefaultAsync(station => station.Id == id
                                             && station.IsActive
                                             && (!station.CompanyId.HasValue
                                                 || _context.Companies.Any(company => company.Id == station.CompanyId && company.IsActive)));
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

    public async Task CreateForCompanyAsync(ChargingStation station, Guid companyId)
    {
        station.CompanyId = companyId;
        await _context.ChargingStations.AddAsync(station);
    }

    public void UpdateForCompany(ChargingStation station)
    {
        var trackedEntry = _context.ChangeTracker.Entries<ChargingStation>()
            .FirstOrDefault(entry => entry.Entity.Id == station.Id);

        if (trackedEntry != null)
        {
            trackedEntry.CurrentValues.SetValues(new
            {
                station.Name,
                station.Location,
                station.Status,
                station.PricePerKwh,
                station.MaxPower,
                station.IsActive,
                station.CompanyId
            });
            return;
        }

        var update = new ChargingStation
        {
            Id = station.Id,
            Name = station.Name,
            Location = station.Location,
            Status = station.Status,
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            IsActive = station.IsActive,
            CompanyId = station.CompanyId
        };

        _context.ChargingStations.Attach(update);
        _context.Entry(update).State = EntityState.Modified;
    }

    public void DeleteForCompany(ChargingStation station)
    {
        _context.ChargingStations.Remove(station);
    }
}
