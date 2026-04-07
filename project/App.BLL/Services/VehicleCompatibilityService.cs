using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;

namespace App.BLL.Services;

public class VehicleCompatibilityService : IVehicleCompatibilityService
{
    private readonly IUnitOfWork _unitOfWork;

    public VehicleCompatibilityService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ChargingStation>> FilterStationsByVehicleAsync(IEnumerable<ChargingStation> stations, Guid vehicleId)
    {
        var compatibleConnectorIds = await _unitOfWork.VehicleConnectors.GetCompatibleConnectorIdsAsync(vehicleId);
        if (compatibleConnectorIds.Count == 0)
        {
            return new List<ChargingStation>();
        }

        var connectorIdSet = compatibleConnectorIds.ToHashSet();

        return stations
            .Where(station => station.ChargingStationConnectors != null
                              && station.ChargingStationConnectors.Any(link => connectorIdSet.Contains(link.ConnectorId)))
            .ToList();
    }

    public async Task<List<Connector>> FilterConnectorsByVehicleAsync(IEnumerable<Connector> connectors, Guid vehicleId)
    {
        var compatibleConnectorIds = await _unitOfWork.VehicleConnectors.GetCompatibleConnectorIdsAsync(vehicleId);
        if (compatibleConnectorIds.Count == 0)
        {
            return new List<Connector>();
        }

        var connectorIdSet = compatibleConnectorIds.ToHashSet();

        return connectors
            .Where(connector => connectorIdSet.Contains(connector.Id))
            .ToList();
    }
}

