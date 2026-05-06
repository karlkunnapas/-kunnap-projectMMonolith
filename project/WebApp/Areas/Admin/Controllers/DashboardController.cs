using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Admin.ViewModels;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root")]
public class DashboardController : Controller
{
    private readonly IAdminPanelService _adminPanelService;

    public DashboardController(IAdminPanelService adminPanelService)
    {
        _adminPanelService = adminPanelService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var normalizedFrom = NormalizeToUtc(fromUtc) ?? DateTime.UtcNow.Date.AddDays(-30);
        var normalizedTo = NormalizeToUtc(toUtc) ?? DateTime.UtcNow;

        var result = await _adminPanelService.GetDashboardAsync(normalizedFrom, normalizedTo);
        if (!result.Success || result.Data == null)
        {
            return BadRequest();
        }

        var model = new AdminDashboardViewModel
        {
            ReservationsInPeriod = result.Data.ReservationsInPeriod,
            TotalCompanies = result.Data.TotalCompanies,
            TotalCompanyUsers = result.Data.TotalCompanyUsers,
            TotalClientUsers = result.Data.TotalClientUsers,
            FromUtc = result.Data.FromUtc,
            ToUtc = result.Data.ToUtc
        };

        return View(model);
    }

    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Local).ToUniversalTime()
        };
    }
}
