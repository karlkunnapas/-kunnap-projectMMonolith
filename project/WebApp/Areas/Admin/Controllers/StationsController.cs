using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Admin.ViewModels;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root")]
public class StationsController : Controller
{
    private readonly IAdminPanelService _adminPanelService;

    public StationsController(IAdminPanelService adminPanelService)
    {
        _adminPanelService = adminPanelService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search = null)
    {
        var result = await _adminPanelService.GetStationsAsync(search);
        if (!result.Success || result.Data == null)
        {
            return BadRequest();
        }

        var model = new AdminStationListViewModel
        {
            Search = result.Data.Search,
            Items = result.Data.Items.Select(item => new AdminStationListItemViewModel
            {
                StationId = item.StationId,
                Name = item.Name,
                Location = item.Location,
                CompanyName = item.CompanyName,
                Status = item.Status,
                IsActive = item.IsActive,
                PricePerKwh = item.PricePerKwh,
                MaxPower = item.MaxPower
            }).ToList()
        };

        return View(model);
    }
}
