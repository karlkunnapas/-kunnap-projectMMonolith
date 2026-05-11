using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Users;

namespace App.BLL.Services;

public class VehicleService : IVehicleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUsersModuleApi _usersModuleApi;

    public VehicleService(IUnitOfWork unitOfWork, IUsersModuleApi usersModuleApi)
    {
        _unitOfWork = unitOfWork;
        _usersModuleApi = usersModuleApi;
    }

    public async Task<ServiceResult<List<VehicleDto>>> GetUserVehiclesAsync(Guid userId)
    {
        var vehicles = await _usersModuleApi.GetUserVehiclesAsync(userId);
        var connectorNameMap = await LoadConnectorNameMapAsync(vehicles.SelectMany(v => v.ConnectorIds));

        var dto = vehicles
            .Select(v => MapVehicle(v, connectorNameMap))
            .ToList();

        return ServiceResult<List<VehicleDto>>.Ok(dto);
    }

    public async Task<ServiceResult<VehicleDto>> GetVehicleForUserAsync(Guid id, Guid userId)
    {
        var vehicle = await _usersModuleApi.GetVehicleForUserAsync(id, userId);
        if (vehicle == null)
        {
            return ServiceResult<VehicleDto>.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        var connectorNameMap = await LoadConnectorNameMapAsync(vehicle.ConnectorIds);
        return ServiceResult<VehicleDto>.Ok(MapVehicle(vehicle, connectorNameMap));
    }

    public async Task<ServiceResult<VehicleDto>> CreateVehicleAsync(Guid userId, VehicleCreateDto dto)
    {
        var created = await _usersModuleApi.CreateVehicleAsync(userId, new CreateUserVehicleContract
        {
            Make = dto.Make,
            Model = dto.Model,
            BatteryCapacity = dto.BatteryCapacity,
            ConnectorIds = dto.ConnectorIds
        });

        var connectorNameMap = await LoadConnectorNameMapAsync(created.ConnectorIds);
        return ServiceResult<VehicleDto>.Ok(MapVehicle(created, connectorNameMap));
    }

    public async Task<ServiceResult<VehicleDto>> UpdateVehicleAsync(Guid id, Guid userId, VehicleUpdateDto dto)
    {
        var vehicle = await _usersModuleApi.UpdateVehicleAsync(id, userId, new UpdateUserVehicleContract
        {
            Make = dto.Make,
            Model = dto.Model,
            BatteryCapacity = dto.BatteryCapacity,
            ConnectorIds = dto.ConnectorIds
        });

        if (vehicle == null)
        {
            return ServiceResult<VehicleDto>.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        var connectorNameMap = await LoadConnectorNameMapAsync(vehicle.ConnectorIds);
        return ServiceResult<VehicleDto>.Ok(MapVehicle(vehicle, connectorNameMap));
    }

    public async Task<ServiceResult> DeleteVehicleAsync(Guid id, Guid userId)
    {
        var deleted = await _usersModuleApi.DeleteVehicleAsync(id, userId);
        if (!deleted)
        {
            return ServiceResult.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetConnectorCompatibilityAsync(Guid vehicleId, Guid userId, IReadOnlyCollection<Guid> connectorIds)
    {
        var updated = await _usersModuleApi.SetConnectorCompatibilityAsync(vehicleId, userId, connectorIds);
        if (!updated)
        {
            return ServiceResult.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<List<VehicleConnectorDto>>> GetCompatibleConnectorsForVehicleAsync(Guid vehicleId, Guid userId)
    {
        var connectorIds = await _usersModuleApi.GetVehicleConnectorIdsAsync(vehicleId, userId);
        if (connectorIds.Count == 0)
        {
            return ServiceResult<List<VehicleConnectorDto>>.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        var connectorNameMap = await LoadConnectorNameMapAsync(connectorIds);
        var connectors = connectorNameMap
            .Select(kvp => BllDtoFactory.CreateVehicleConnectorDto(kvp.Key, kvp.Value))
            .OrderBy(c => c.Name)
            .ToList();

        return ServiceResult<List<VehicleConnectorDto>>.Ok(connectors);
    }

    public async Task<ServiceResult<List<CompatibleStationDto>>> GetCompatibleStationsForVehicleAsync(Guid vehicleId, Guid userId)
    {
        var connectorIds = (await _usersModuleApi.GetVehicleConnectorIdsAsync(vehicleId, userId)).ToHashSet();
        if (connectorIds.Count == 0)
        {
            return ServiceResult<List<CompatibleStationDto>>.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        var stations = await _unitOfWork.ChargingStations
            .GetStationsWithConnectors()
            .Where(s => s.IsActive && s.Company != null && s.Company.IsActive)
            .ToListAsync();

        var compatible = stations
            .Where(station => station.ChargingStationConnectors != null
                              && station.ChargingStationConnectors.Any(link => connectorIds.Contains(link.ConnectorId)))
            .Select(station => BllDtoFactory.CreateCompatibleStationDto(
                station,
                station.ChargingStationConnectors!
                    .Where(link => link.Connector != null && connectorIds.Contains(link.ConnectorId))
                    .Select(link => link.Connector!.Name.Translate() ?? link.Connector.Name.ToString() ?? string.Empty)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList()))
            .ToList();

        return ServiceResult<List<CompatibleStationDto>>.Ok(compatible);
    }

    private async Task<Dictionary<Guid, string>> LoadConnectorNameMapAsync(IEnumerable<Guid> connectorIds)
    {
        var ids = connectorIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var connectors = await _unitOfWork.Connectors
            .GetQueryable()
            .Where(c => c.IsActive && ids.Contains(c.Id))
            .ToListAsync();

        return connectors.ToDictionary(
            c => c.Id,
            c => c.Name.Translate() ?? c.Name.ToString() ?? string.Empty);
    }

    private static VehicleDto MapVehicle(UserVehicleContract vehicle, IReadOnlyDictionary<Guid, string> connectorNameMap)
    {
        var connectors = vehicle.ConnectorIds
            .Where(connectorNameMap.ContainsKey)
            .Select(id => BllDtoFactory.CreateVehicleConnectorDto(id, connectorNameMap[id]))
            .OrderBy(c => c.Name)
            .ToList();

        return new VehicleDto
        {
            Id = vehicle.VehicleId,
            Make = vehicle.Make,
            Model = vehicle.Model,
            BatteryCapacity = vehicle.BatteryCapacity,
            CompatibleConnectors = connectors
        };
    }
}
