using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.Domain;
using System.Globalization;
using Shared.Contracts.Charging;
using Shared.Contracts.Users;

namespace App.BLL.Services;

public class ChargingStationService : IChargingStationService
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly IUsersModuleApi _usersModuleApi;

    public ChargingStationService(
        IChargingModuleApi chargingModuleApi,
        IUsersModuleApi usersModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _usersModuleApi = usersModuleApi;
    }

    public async Task<ServiceResult<HomePageDto>> GetHomePageAsync(HomePageFilterDto? filters = null)
    {
        var stations = await _chargingModuleApi.GetStationsForHomeAsync(ParseStatus(filters?.Status));
        var stationNamesByLanguage = stations.ToDictionary(station => station.Id, GetNameTranslations);

        HashSet<Guid>? vehicleConnectorIds = null;
        if (filters?.VehicleId is Guid vehicleId && filters.UserId is Guid userId)
        {
            var vehicle = await _usersModuleApi.GetVehicleForUserAsync(vehicleId, userId);
            if (vehicle == null)
            {
                return ServiceResult<HomePageDto>.Fail("FORBIDDEN", "Vehicle was not found for user.");
            }

            vehicleConnectorIds = vehicle.ConnectorIds.ToHashSet();
        }

        if (vehicleConnectorIds != null)
        {
            stations = stations
                .Where(station => station.Connectors.Any(link => vehicleConnectorIds.Contains(link.Id)))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(filters?.Location))
        {
            var locationOrNameFilter = filters.Location.Trim();
            stations = stations
                .Where(station =>
                    station.Location.Contains(locationOrNameFilter, StringComparison.OrdinalIgnoreCase) ||
                    MatchesStationNameFilter(station, stationNamesByLanguage, locationOrNameFilter))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(filters?.Connector))
        {
            var connectorFilter = filters.Connector.Trim();
            stations = stations
                .Where(station => station.Connectors
                    .Any(link => link.IsActive
                                 && string.Equals(link.Name, connectorFilter, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

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
                Name = GetLocalizedDisplayName(station, stationNamesByLanguage),
                NameTranslations = stationNamesByLanguage[station.Id],
                Location = station.Location,
                Status = MapStatus(station.Status),
                PricePerKwh = station.PricePerKwh,
                MaxPower = station.MaxPower,
                ConnectorNames = station.Connectors
                    .Where(link => link.IsActive)
                    .Select(link => link.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .ToList(),
                IsCompatibleWithSelectedVehicle = vehicleConnectorIds == null
                    ? null
                    : station.Connectors.Any(link => vehicleConnectorIds.Contains(link.Id))
            })
            .ToList();

        var dto = BllDtoFactory.CreateHomePageDto(stationDtos, connectorFilters);

        return ServiceResult<HomePageDto>.Ok(dto);
    }

    private static bool MatchesStationNameFilter(
        ChargingStationContract station,
        IReadOnlyDictionary<Guid, Dictionary<string, string>> stationNamesByLanguage,
        string filter)
    {
        if (station.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stationNamesByLanguage.TryGetValue(station.Id, out var translations)
               && translations.Values.Any(name => !string.IsNullOrWhiteSpace(name)
                                                  && name.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetLocalizedDisplayName(
        ChargingStationContract station,
        IReadOnlyDictionary<Guid, Dictionary<string, string>> stationNamesByLanguage)
    {
        if (!stationNamesByLanguage.TryGetValue(station.Id, out var translations) || translations.Count == 0)
        {
            return station.Name;
        }

        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        if (translations.TryGetValue(culture, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (translations.TryGetValue("en", out var english) && !string.IsNullOrWhiteSpace(english))
        {
            return english;
        }

        return translations.Values.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? station.Name;
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

    private static Shared.Contracts.Charging.EStationStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (Enum.TryParse<Shared.Contracts.Charging.EStationStatus>(status, true, out var parsed))
        {
            return parsed;
        }

        return status.Trim() switch
        {
            "0" => Shared.Contracts.Charging.EStationStatus.Available,
            "2" => Shared.Contracts.Charging.EStationStatus.InUse,
            "3" => Shared.Contracts.Charging.EStationStatus.Maintenance,
            _ => null
        };
    }

    private static App.Domain.EStationStatus MapStatus(Shared.Contracts.Charging.EStationStatus status)
    {
        return status switch
        {
            Shared.Contracts.Charging.EStationStatus.Available => App.Domain.EStationStatus.Available,
            Shared.Contracts.Charging.EStationStatus.InUse => App.Domain.EStationStatus.InUse,
            Shared.Contracts.Charging.EStationStatus.Maintenance => App.Domain.EStationStatus.Maintenance,
            _ => App.Domain.EStationStatus.Available
        };
    }
}
