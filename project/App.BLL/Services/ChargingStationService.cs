using System;
using System.Linq;
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

    public async Task<ServiceResult<HomePageDto>> GetHomePageAsync()
    {
        var stations = await _unitOfWork.ChargingStations
            .GetStationsWithConnectors()
            .Where(station => station.IsActive)
            .ToListAsync();

        var connectorFilters = stations
            .SelectMany(station => station.ChargingStationConnectors ?? Array.Empty<ChargingStationConnector>())
            .Where(link => link.Connector != null && link.Connector.IsActive)
            .Select(link => link.Connector?.Name?.Translate() ?? link.Connector?.Name?.ToString() ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        var stationDtos = stations
            .Select(station => new HomeStationDto
            {
                Id = station.Id,
                Name = station.Name?.Translate() ?? station.Name?.ToString() ?? string.Empty,
                Location = station.Location,
                Status = station.Status,
                PricePerHour = station.PricePerHour,
                MaxPower = station.MaxPower,
                ConnectorNames = station.ChargingStationConnectors
                    ?.Where(link => link.Connector != null && link.Connector.IsActive)
                    .Select(link => link.Connector?.Name?.Translate() ?? link.Connector?.Name?.ToString() ?? string.Empty)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .ToList() ?? new List<string>()
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
