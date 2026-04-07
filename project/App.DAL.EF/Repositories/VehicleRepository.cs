using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories;

public class VehicleRepository : IVehicleRepository
{
    private readonly AppDbContext _context;

    public VehicleRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<Vehicle>> GetByUserIdAsync(Guid userId)
    {
        return _context.Vehicles
            .Include(v => v.VehicleConnectors!)
            .ThenInclude(vc => vc.Connector!)
            .Where(v => v.UserId == userId)
            .OrderBy(v => v.Make)
            .ThenBy(v => v.Model)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<Vehicle?> GetByIdWithConnectorsAsync(Guid id)
    {
        return _context.Vehicles
            .Include(v => v.VehicleConnectors!)
            .ThenInclude(vc => vc.Connector!)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public Task<Vehicle?> GetByIdForUserAsync(Guid id, Guid userId)
    {
        return _context.Vehicles
            .Include(v => v.VehicleConnectors!)
            .ThenInclude(vc => vc.Connector!)
            .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);
    }

    public Task<Vehicle?> GetByIdForUserForUpdateAsync(Guid id, Guid userId)
    {
        return _context.Vehicles
            .AsTracking()
            .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);
    }

    public Task AddAsync(Vehicle vehicle)
    {
        return _context.Vehicles.AddAsync(vehicle).AsTask();
    }

    public void Update(Vehicle vehicle)
    {
        _context.Vehicles.Update(vehicle);
    }

    public void Remove(Vehicle vehicle)
    {
        _context.Vehicles.Remove(vehicle);
    }
}
