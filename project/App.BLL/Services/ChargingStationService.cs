using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.BLL.Services;

public class ChargingStationService : IChargingStationService
{
    private readonly IUnitOfWork _unitOfWork;

    public ChargingStationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<HomePageDto>> GetHomePageAsync(HomePageFilterDto? filters = null)
    {
        var stations = await _unitOfWork.ChargingStations
            .GetStationsForHome(filters?.Status, null)
            .ToListAsync();

        HashSet<Guid>? vehicleConnectorIds = null;
        if (filters?.VehicleId is Guid vehicleId)
        {
            var connectorIds = await _unitOfWork.VehicleConnectors.GetCompatibleConnectorIdsAsync(vehicleId);
            vehicleConnectorIds = connectorIds.ToHashSet();
            stations = stations
                .Where(station => station.ChargingStationConnectors != null
                                  && station.ChargingStationConnectors.Any(link => vehicleConnectorIds.Contains(link.ConnectorId)))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(filters?.Location))
        {
            var locationOrNameFilter = filters.Location.Trim();
            stations = stations
                .Where(station =>
                    station.Location.Contains(locationOrNameFilter, StringComparison.OrdinalIgnoreCase) ||
                    (station.Name.Translate() ?? station.Name.ToString() ?? string.Empty)
                    .Contains(locationOrNameFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(filters?.Connector))
        {
            var connectorFilter = filters.Connector.Trim();
            stations = stations
                .Where(station => station.ChargingStationConnectors
                    ?.Any(link => link.Connector != null
                                  && link.Connector.IsActive
                                  && string.Equals(
                                      link.Connector.Name.Translate() ?? link.Connector.Name.ToString(),
                                      connectorFilter,
                                      StringComparison.OrdinalIgnoreCase)) == true)
                .ToList();
        }

        var connectorFilters = stations
            .SelectMany(station => station.ChargingStationConnectors ?? Array.Empty<ChargingStationConnector>())
            .Where(link => link.Connector != null && link.Connector.IsActive)
            .Select(link => link.Connector!.Name.Translate() ?? link.Connector.Name.ToString() ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        var stationDtos = stations
            .Select(station => new HomeStationDto
            {
                Id = station.Id,
                Name = station.Name.Translate() ?? station.Name.ToString() ?? string.Empty,
                Location = station.Location,
                Status = station.Status,
                PricePerHour = station.PricePerHour,
                MaxPower = station.MaxPower,
                ConnectorNames = station.ChargingStationConnectors
                    ?.Where(link => link.Connector != null && link.Connector.IsActive)
                    .Select(link => link.Connector!.Name.Translate() ?? link.Connector.Name.ToString() ?? string.Empty)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .ToList() ?? new List<string>(),
                IsCompatibleWithSelectedVehicle = vehicleConnectorIds == null
                    ? null
                    : station.ChargingStationConnectors != null
                      && station.ChargingStationConnectors.Any(link => vehicleConnectorIds.Contains(link.ConnectorId))
            })
            .ToList();

        var dto = new HomePageDto
        {
            Stations = stationDtos,
            ConnectorFilters = connectorFilters
        };

        return ServiceResult<HomePageDto>.Ok(dto);
    }
}
