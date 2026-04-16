using System.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels;
using WebAppClient.Helpers;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Controllers;

public class HomeController : Controller
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IApiClient apiClient, ILogger<HomeController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? status = null, string? connector = null, string? location = null, Guid? vehicleId = null)
    {
        var model = new HomeIndexViewModel
        {
            SelectedStatus = status,
            SelectedConnector = connector,
            LocationQuery = location,
            SelectedVehicleId = vehicleId,
            ShowVehicleFilters = User.IsInRole("Customer")
        };

        try
        {
            var stations = await _apiClient.GetAsync<List<StationSummaryDto>>("api/v1/station");
            var filtered = stations.AsEnumerable();
            HashSet<string>? selectedVehicleConnectorNames = null;

            if (!string.IsNullOrWhiteSpace(status))
            {
                filtered = filtered.Where(s => string.Equals(s.Status, status, StringComparison.OrdinalIgnoreCase));
            }

            if (model.ShowVehicleFilters)
            {
                try
                {
                    var vehicles = await _apiClient.GetAsync<List<VehicleResponseDto>>("api/v1/vehicle");
                    model.VehicleOptions = vehicles
                        .Select(v => new HomeVehicleOptionViewModel
                        {
                            Id = v.Id,
                            DisplayName = $"{v.Make} {v.Model}"
                        })
                        .ToList();

                    if (vehicleId.HasValue)
                    {
                        var selectedVehicle = vehicles.FirstOrDefault(v => v.Id == vehicleId.Value);
                        if (selectedVehicle == null)
                        {
                            model.SelectedVehicleId = null;
                        }
                        else
                        {
                            selectedVehicleConnectorNames = selectedVehicle.CompatibleConnectors
                                .Select(c => c.Name)
                                .Where(name => !string.IsNullOrWhiteSpace(name))
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);

                            filtered = filtered.Where(s =>
                                s.ConnectorNames.Any(c => selectedVehicleConnectorNames.Contains(c)));
                        }
                    }
                }
                catch (ApiUnauthorizedException ex)
                {
                    _logger.LogWarning(ex, "Vehicle filter data unauthorized. Showing stations without vehicle filtering.");
                    model.SelectedVehicleId = null;
                    model.VehicleOptions = new List<HomeVehicleOptionViewModel>();
                }
            }

            if (!string.IsNullOrWhiteSpace(connector))
            {
                filtered = filtered.Where(s => s.ConnectorNames.Any(c => string.Equals(c, connector, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                filtered = filtered.Where(s => s.Location.Contains(location, StringComparison.OrdinalIgnoreCase)
                                               || s.Name.Contains(location, StringComparison.OrdinalIgnoreCase));
            }

            model.ConnectorFilters = stations.SelectMany(s => s.ConnectorNames).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
            model.Stations = filtered.Select(s => new HomeStationViewModel
            {
                Id = s.Id,
                Name = LocalizationHelper.GetLocalizedName(s.Name, s.NameTranslations),
                Location = s.Location,
                Status = EnumParser.ParseStation(s.Status),
                PricePerKwh = s.PricePerKwh,
                MaxPower = s.MaxPower,
                ConnectorNames = s.ConnectorNames,
                IsCompatibleWithSelectedVehicle = selectedVehicleConnectorNames == null
                    ? s.IsCompatibleWithSelectedVehicle
                    : s.ConnectorNames.Any(c => selectedVehicleConnectorNames.Contains(c))
            }).ToList();
        }
        catch (ApiException ex)
        {
            _logger.LogWarning(ex, "Failed to load home page station data from API.");
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return LocalRedirect(returnUrl);
        }

        var requestCulture = new RequestCulture(culture);
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(requestCulture),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });

        return LocalRedirect(returnUrl);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new WebApp.ViewModels.ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
