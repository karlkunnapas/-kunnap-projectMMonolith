using App.Domain;

namespace App.DAL.EF.Repositories.Interfaces;

public interface IVehicleRepository
{
    Task<List<Vehicle>> GetByUserIdAsync(Guid userId);
    Task<Vehicle?> GetByIdWithConnectorsAsync(Guid id);
    Task<Vehicle?> GetByIdForUserAsync(Guid id, Guid userId);
    Task<Vehicle?> GetByIdForUserForUpdateAsync(Guid id, Guid userId);
    Task AddAsync(Vehicle vehicle);
    void Update(Vehicle vehicle);
    void Remove(Vehicle vehicle);
}
