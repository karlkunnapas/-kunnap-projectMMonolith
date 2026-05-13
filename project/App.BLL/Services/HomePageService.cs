using System;
using System.Linq;
using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.Domain;
using Shared.Contracts.Charging;

namespace App.BLL.Services;

public class HomePageService : IHomePageService
{
    private readonly IChargingModuleApi _chargingModuleApi;

    public HomePageService(IChargingModuleApi chargingModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
    }

    public async Task<ServiceResult<HomePageDto>> GetCustomerHomePageAsync()
    {
        var stations = (await _chargingModuleApi.GetStationsForHomeAsync())
            .Where(s => s.IsActive)
            .ToList();
        var stationNamesByLanguage = stations.ToDictionary(station => station.Id, GetNameTranslations);

        var connectorFilters = stations
            .SelectMany(station => station.Connectors)
            .Where(link => link.IsActive)
            .Select(link => link.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        var stationDtos = stations
            .Select(station => new HomeStationDto
            {
                Id = station.Id,
                Name = station.Name,
                NameTranslations = stationNamesByLanguage[station.Id],
                Location = station.Location,
                Status = station.Status switch
                {
                    Shared.Contracts.Charging.EStationStatus.Available => App.Domain.EStationStatus.Available,
                    Shared.Contracts.Charging.EStationStatus.InUse => App.Domain.EStationStatus.InUse,
                    Shared.Contracts.Charging.EStationStatus.Maintenance => App.Domain.EStationStatus.Maintenance,
                    _ => App.Domain.EStationStatus.Available
                },
                PricePerKwh = station.PricePerKwh,
                MaxPower = station.MaxPower,
                ConnectorNames = station.Connectors
                    .Where(link => link.IsActive)
                    .Select(link => link.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .ToList(),
                IsCompatibleWithSelectedVehicle = null
            })
            .ToList();

        var dto = BllDtoFactory.CreateHomePageDto(stationDtos, connectorFilters);

        return ServiceResult<HomePageDto>.Ok(dto);
    }

    private static Dictionary<string, string> GetNameTranslations(ChargingStationContract station)
    {
        if (station.NameTranslations.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return station.NameTranslations
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key.Trim().ToLowerInvariant(), pair => pair.Value.Trim());
    }
}
