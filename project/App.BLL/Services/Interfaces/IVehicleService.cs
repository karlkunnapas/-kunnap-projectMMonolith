using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IVehicleService
{
    Task<ServiceResult<List<VehicleDto>>> GetUserVehiclesAsync(Guid userId);
    Task<ServiceResult<VehicleDto>> GetVehicleForUserAsync(Guid id, Guid userId);
    Task<ServiceResult<VehicleDto>> CreateVehicleAsync(Guid userId, VehicleCreateDto dto);
    Task<ServiceResult<VehicleDto>> UpdateVehicleAsync(Guid id, Guid userId, VehicleUpdateDto dto);
    Task<ServiceResult> DeleteVehicleAsync(Guid id, Guid userId);
    Task<ServiceResult> SetConnectorCompatibilityAsync(Guid vehicleId, Guid userId, IReadOnlyCollection<Guid> connectorIds);
    Task<ServiceResult<List<VehicleConnectorDto>>> GetCompatibleConnectorsForVehicleAsync(Guid vehicleId, Guid userId);
    Task<ServiceResult<List<CompatibleStationDto>>> GetCompatibleStationsForVehicleAsync(Guid vehicleId, Guid userId);
}

