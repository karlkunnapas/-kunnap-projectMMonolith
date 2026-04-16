using App.Domain;

namespace App.DAL.EF.Repositories.Interfaces;

public interface IVehicleConnectorRepository
{
    Task<List<VehicleConnector>> GetByVehicleIdAsync(Guid vehicleId);
    Task<List<Guid>> GetCompatibleConnectorIdsAsync(Guid vehicleId);
    Task ReplaceCompatibilityAsync(Guid vehicleId, IReadOnlyCollection<Guid> connectorIds);
    Task RemoveAllForVehicleAsync(Guid vehicleId);
}
