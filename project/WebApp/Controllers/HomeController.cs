using System.Diagnostics;
using System.Security.Claims;
using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels;

namespace WebApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IChargingStationService _chargingStationService;
    private readonly IVehicleService _vehicleService;

    public HomeController(IChargingStationService chargingStationService, IVehicleService vehicleService, ILogger<HomeController> logger)
    {
        _logger = logger;
        _chargingStationService = chargingStationService;
        _vehicleService = vehicleService;
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
                var ownedVehicleResult = await _vehicleService.GetVehicleForUserAsync(vehicleId.Value, currentUserId.Value);
                if (!ownedVehicleResult.Success)
                {
                    return Forbid();
                }
            }
        }

        var filters = BllDtoFactory.CreateHomePageFilterDto(status, connector, location, vehicleId);

        var result = await _chargingStationService.GetHomePageAsync(filters);
        if (!result.Success || result.Data == null)
        {
            _logger.LogWarning("Home page data load failed: {Errors}",
                string.Join("; ", result.Errors.Select(error => error.Message)));

            return View(new HomeIndexViewModel
            {
                SelectedStatus = status,
                SelectedConnector = connector,
                LocationQuery = location,
                SelectedVehicleId = vehicleId
            });
        }

        var vehicleOptions = new List<HomeVehicleOptionViewModel>();
        if (showVehicleFilters && currentUserId != null)
        {
            var vehiclesResult = await _vehicleService.GetUserVehiclesAsync(currentUserId.Value);
            vehicleOptions = vehiclesResult.Data?
                .Select(v => new HomeVehicleOptionViewModel
                {
                    Id = v.Id,
                    DisplayName = $"{v.Make} {v.Model}"
                })
                .ToList() ?? new List<HomeVehicleOptionViewModel>();
        }

        var viewModel = new HomeIndexViewModel
        {
            ShowVehicleFilters = showVehicleFilters,
            ConnectorFilters = showVehicleFilters ? result.Data.ConnectorFilters.ToList() : new List<string>(),
            SelectedStatus = status,
            SelectedConnector = connector,
            LocationQuery = location,
            SelectedVehicleId = vehicleId,
            VehicleOptions = vehicleOptions,
            Stations = result.Data.Stations.Select(station => new HomeStationViewModel
            {
                Id = station.Id,
                Name = station.Name,
                Location = station.Location,
                Status = station.Status,
                PricePerKwh = station.PricePerKwh,
                MaxPower = station.MaxPower,
                ConnectorNames = station.ConnectorNames,
                IsCompatibleWithSelectedVehicle = station.IsCompatibleWithSelectedVehicle
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
