using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.BLL.Services;

public class VehicleService : IVehicleService
{
    private readonly IUnitOfWork _unitOfWork;

    public VehicleService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<List<VehicleDto>>> GetUserVehiclesAsync(Guid userId)
    {
        var vehicles = await _unitOfWork.Vehicles.GetByUserIdAsync(userId);
        return ServiceResult<List<VehicleDto>>.Ok(vehicles.Select(MapVehicle).ToList());
    }

    public async Task<ServiceResult<VehicleDto>> GetVehicleForUserAsync(Guid id, Guid userId)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdForUserAsync(id, userId);
        if (vehicle == null)
        {
            return ServiceResult<VehicleDto>.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        return ServiceResult<VehicleDto>.Ok(MapVehicle(vehicle));
    }

    public async Task<ServiceResult<VehicleDto>> CreateVehicleAsync(Guid userId, VehicleCreateDto dto)
    {
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Make = dto.Make.Trim(),
            Model = dto.Model.Trim(),
            BatteryCapacity = dto.BatteryCapacity
        };

        await _unitOfWork.Vehicles.AddAsync(vehicle);
        await _unitOfWork.VehicleConnectors.ReplaceCompatibilityAsync(vehicle.Id, dto.ConnectorIds);
        await _unitOfWork.SaveAsync();

        var created = await _unitOfWork.Vehicles.GetByIdForUserAsync(vehicle.Id, userId);
        return ServiceResult<VehicleDto>.Ok(MapVehicle(created!));
    }

    public async Task<ServiceResult<VehicleDto>> UpdateVehicleAsync(Guid id, Guid userId, VehicleUpdateDto dto)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdForUserForUpdateAsync(id, userId);
        if (vehicle == null)
        {
            return ServiceResult<VehicleDto>.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        vehicle.Make = dto.Make.Trim();
        vehicle.Model = dto.Model.Trim();
        vehicle.BatteryCapacity = dto.BatteryCapacity;

        // Keep tracked scalar updates and compatibility replacement separate to avoid graph tracking conflicts.
        await _unitOfWork.VehicleConnectors.ReplaceCompatibilityAsync(vehicle.Id, dto.ConnectorIds);
        await _unitOfWork.SaveAsync();

        var updated = await _unitOfWork.Vehicles.GetByIdForUserAsync(id, userId);
        return ServiceResult<VehicleDto>.Ok(MapVehicle(updated!));
    }

    public async Task<ServiceResult> DeleteVehicleAsync(Guid id, Guid userId)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdForUserAsync(id, userId);
        if (vehicle == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        _unitOfWork.Vehicles.Remove(vehicle);
        await _unitOfWork.SaveAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetConnectorCompatibilityAsync(Guid vehicleId, Guid userId, IReadOnlyCollection<Guid> connectorIds)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdForUserAsync(vehicleId, userId);
        if (vehicle == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        await _unitOfWork.VehicleConnectors.ReplaceCompatibilityAsync(vehicleId, connectorIds);
        await _unitOfWork.SaveAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<List<VehicleConnectorDto>>> GetCompatibleConnectorsForVehicleAsync(Guid vehicleId, Guid userId)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdForUserAsync(vehicleId, userId);
        if (vehicle == null)
        {
            return ServiceResult<List<VehicleConnectorDto>>.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        var connectors = vehicle.VehicleConnectors?
            .Where(vc => vc.Connector != null && vc.Connector.IsActive)
            .Select(vc => new VehicleConnectorDto
            {
                ConnectorId = vc.ConnectorId,
                Name = vc.Connector!.Name.Translate() ?? vc.Connector.Name.ToString() ?? string.Empty
            })
            .OrderBy(c => c.Name)
            .ToList() ?? new List<VehicleConnectorDto>();

        return ServiceResult<List<VehicleConnectorDto>>.Ok(connectors);
    }

    public async Task<ServiceResult<List<CompatibleStationDto>>> GetCompatibleStationsForVehicleAsync(Guid vehicleId, Guid userId)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdForUserAsync(vehicleId, userId);
        if (vehicle == null)
        {
            return ServiceResult<List<CompatibleStationDto>>.Fail("FORBIDDEN", "Vehicle not found or access denied.");
        }

        var connectorIds = vehicle.VehicleConnectors?.Select(vc => vc.ConnectorId).Distinct().ToHashSet() ?? new HashSet<Guid>();
        if (connectorIds.Count == 0)
        {
            return ServiceResult<List<CompatibleStationDto>>.Ok(new List<CompatibleStationDto>());
        }

        var stations = await _unitOfWork.ChargingStations.GetStationsWithConnectors().Where(s => s.IsActive).ToListAsync();

        var compatible = stations
            .Where(station => station.ChargingStationConnectors != null
                              && station.ChargingStationConnectors.Any(link => connectorIds.Contains(link.ConnectorId)))
            .Select(station => new CompatibleStationDto
            {
                StationId = station.Id,
                StationName = station.Name.Translate() ?? station.Name.ToString() ?? string.Empty,
                Location = station.Location,
                Status = station.Status,
                CompatibleConnectorNames = station.ChargingStationConnectors!
                    .Where(link => link.Connector != null && connectorIds.Contains(link.ConnectorId))
                    .Select(link => link.Connector!.Name.Translate() ?? link.Connector.Name.ToString() ?? string.Empty)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList()
            })
            .ToList();

        return ServiceResult<List<CompatibleStationDto>>.Ok(compatible);
    }

    private static VehicleDto MapVehicle(Vehicle vehicle)
    {
        return new VehicleDto
        {
            Id = vehicle.Id,
            Make = vehicle.Make,
            Model = vehicle.Model,
            BatteryCapacity = vehicle.BatteryCapacity,
            CompatibleConnectors = vehicle.VehicleConnectors?
                .Where(vc => vc.Connector != null && vc.Connector.IsActive)
                .Select(vc => new VehicleConnectorDto
                {
                    ConnectorId = vc.ConnectorId,
                    Name = vc.Connector!.Name.Translate() ?? vc.Connector.Name.ToString() ?? string.Empty
                })
                .OrderBy(c => c.Name)
                .ToList() ?? new List<VehicleConnectorDto>()
        };
    }
}
