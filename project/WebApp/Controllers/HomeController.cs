using System.Diagnostics;
using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels;

namespace WebApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IChargingStationService _chargingStationService;

    public HomeController(IChargingStationService chargingStationService, ILogger<HomeController> logger)
    {
        _logger = logger;
        _chargingStationService = chargingStationService;
    }

    public async Task<IActionResult> Index(string? status = null, string? connector = null, string? location = null)
    {
        var filters = new HomePageFilterDto
        {
            Status = status,
            Connector = connector,
            Location = location
        };

        var result = await _chargingStationService.GetHomePageAsync(filters);
        if (!result.Success || result.Data == null)
        {
            _logger.LogWarning("Home page data load failed: {Errors}",
                string.Join("; ", result.Errors.Select(error => error.Message)));

            return View(new HomeIndexViewModel
            {
                SelectedStatus = status,
                SelectedConnector = connector,
                LocationQuery = location
            });
        }

        var showVehicleFilters = HttpContext?.User?.IsInRole("Customer") == true;

        var viewModel = new HomeIndexViewModel
        {
            ShowVehicleFilters = showVehicleFilters,
            ConnectorFilters = showVehicleFilters ? result.Data.ConnectorFilters.ToList() : new List<string>(),
            SelectedStatus = status,
            SelectedConnector = connector,
            LocationQuery = location,
            Stations = result.Data.Stations.Select(station => new HomeStationViewModel
            {
                Id = station.Id,
                Name = station.Name,
                Location = station.Location,
                Status = station.Status,
                PricePerHour = station.PricePerHour,
                MaxPower = station.MaxPower,
                ConnectorNames = station.ConnectorNames
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