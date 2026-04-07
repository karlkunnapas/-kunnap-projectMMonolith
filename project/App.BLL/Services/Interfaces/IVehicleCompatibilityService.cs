using App.Domain;

namespace App.BLL.Services.Interfaces;

public interface IVehicleCompatibilityService
{
    Task<List<ChargingStation>> FilterStationsByVehicleAsync(IEnumerable<ChargingStation> stations, Guid vehicleId);
    Task<List<Connector>> FilterConnectorsByVehicleAsync(IEnumerable<Connector> connectors, Guid vehicleId);
}

