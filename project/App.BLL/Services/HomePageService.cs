using System;
using System.Linq;
using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.BLL.Services;

public class HomePageService : IHomePageService
{
    private readonly IUnitOfWork _unitOfWork;

    public HomePageService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<HomePageDto>> GetCustomerHomePageAsync()
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
                PricePerKwh = station.PricePerKwh,
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
