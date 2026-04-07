using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories;

public class VehicleConnectorRepository : IVehicleConnectorRepository
{
    private readonly AppDbContext _context;

    public VehicleConnectorRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<VehicleConnector>> GetByVehicleIdAsync(Guid vehicleId)
    {
        return _context.VehicleConnectors
            .Include(vc => vc.Connector!)
            .Where(vc => vc.VehicleId == vehicleId)
            .ToListAsync();
    }

    public Task<List<Guid>> GetCompatibleConnectorIdsAsync(Guid vehicleId)
    {
        return _context.VehicleConnectors
            .Where(vc => vc.VehicleId == vehicleId)
            .Select(vc => vc.ConnectorId)
            .Distinct()
            .ToListAsync();
    }

    public async Task ReplaceCompatibilityAsync(Guid vehicleId, IReadOnlyCollection<Guid> connectorIds)
    {
        var existing = await _context.VehicleConnectors
            .Where(vc => vc.VehicleId == vehicleId)
            .ToListAsync();

        _context.VehicleConnectors.RemoveRange(existing);

        foreach (var connectorId in connectorIds.Distinct())
        {
            await _context.VehicleConnectors.AddAsync(new VehicleConnector
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicleId,
                ConnectorId = connectorId
            });
        }
    }
}

