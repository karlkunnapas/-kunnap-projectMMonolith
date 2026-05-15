using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Users;
using WebApp.ViewModels;
using ContractStationStatus = Shared.Contracts.Charging.EStationStatus;
using DomainStationStatus = App.Domain.EStationStatus;

namespace WebApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly IUsersModuleApi _usersModuleApi;

    public HomeController(IChargingModuleApi chargingModuleApi, IUsersModuleApi usersModuleApi, ILogger<HomeController> logger)
    {
        _logger = logger;
        _chargingModuleApi = chargingModuleApi;
        _usersModuleApi = usersModuleApi;
    }

    public async Task<IActionResult> Index(string? status = null, string? connector = null, string? location = null, Guid? vehicleId = null)
    {
        var showVehicleFilters = HttpContext?.User?.IsInRole("Customer") == true;

        Guid? currentUserId = null;
        if (showVehicleFilters)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdClaim, out var parsedUserId))
            {
                currentUserId = parsedUserId;
            }
        }

        if (vehicleId.HasValue)
        {
            if (!showVehicleFilters || currentUserId == null)
            {
                vehicleId = null;
            }
            else
            {
                var ownedVehicle = await _usersModuleApi.GetVehicleForUserAsync(vehicleId.Value, currentUserId.Value);
                if (ownedVehicle == null)
                {
                    return Forbid();
                }
            }
        }

        ContractStationStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ContractStationStatus>(status, true, out var parsed))
        {
            parsedStatus = parsed;
        }

        var stations = await _chargingModuleApi.GetStationsForHomeAsync(parsedStatus);
        IReadOnlyCollection<Guid> selectedVehicleConnectorIds = Array.Empty<Guid>();
        if (showVehicleFilters && currentUserId != null && vehicleId.HasValue)
        {
            selectedVehicleConnectorIds = await _usersModuleApi.GetVehicleConnectorIdsAsync(vehicleId.Value, currentUserId.Value);
        }

        var filteredStations = stations.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(connector))
        {
            filteredStations = filteredStations.Where(s =>
                s.Connectors.Any(c => c.Name.Contains(connector, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            filteredStations = filteredStations.Where(s =>
                s.Location.Contains(location, StringComparison.OrdinalIgnoreCase));
        }

        if (vehicleId.HasValue && selectedVehicleConnectorIds.Count > 0)
        {
            filteredStations = filteredStations.Where(s =>
                s.Connectors.Any(c => selectedVehicleConnectorIds.Contains(c.Id)));
        }

        var filteredStationList = filteredStations.ToList();

        var vehicleOptions = new List<HomeVehicleOptionViewModel>();
        if (showVehicleFilters && currentUserId != null)
        {
            var vehicles = await _usersModuleApi.GetUserVehiclesAsync(currentUserId.Value);
            vehicleOptions = vehicles
                .Select(v => new HomeVehicleOptionViewModel
                {
                    Id = v.VehicleId,
                    DisplayName = $"{v.Make} {v.Model}"
                })
                .ToList();
        }

        var connectorFilters = filteredStationList
            .SelectMany(s => s.Connectors.Select(c => c.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        var viewModel = new HomeIndexViewModel
        {
            ShowVehicleFilters = showVehicleFilters,
            ConnectorFilters = showVehicleFilters ? connectorFilters : new List<string>(),
            SelectedStatus = status,
            SelectedConnector = connector,
            LocationQuery = location,
            SelectedVehicleId = vehicleId,
            VehicleOptions = vehicleOptions,
            Stations = filteredStationList.Select(station => new HomeStationViewModel
            {
                Id = station.Id,
                Name = station.Name,
                Location = station.Location,
                Status = (DomainStationStatus)(int)station.Status,
                PricePerKwh = station.PricePerKwh,
                MaxPower = station.MaxPower,
                ConnectorNames = station.Connectors.Select(c => c.Name).ToList(),
                IsCompatibleWithSelectedVehicle = vehicleId.HasValue
                    ? station.Connectors.Any(c => selectedVehicleConnectorIds.Contains(c.Id))
                    : null
            }).ToList()
        };

        return View(viewModel);
    }


    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        try
        {
            var reqCulture = new RequestCulture(culture);

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(reqCulture),
                new CookieOptions()
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1)
                }
            );
        }
        catch (Exception e)
        {
            _logger.LogError("SetLanguage exception: {}", e.Message);
        }

        return LocalRedirect(returnUrl);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
