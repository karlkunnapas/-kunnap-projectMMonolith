using App.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using WebApp.Areas.Admin.ViewModels;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root")]
public class DashboardController : Controller
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private readonly UserManager<AppUser> _userManager;

    public DashboardController(
        IChargingModuleApi chargingModuleApi,
        ICompaniesModuleApi companiesModuleApi,
        UserManager<AppUser> userManager)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var normalizedFrom = NormalizeToUtc(fromUtc) ?? DateTime.UtcNow.Date.AddDays(-30);
        var normalizedTo = NormalizeToUtc(toUtc) ?? DateTime.UtcNow;

        var reservationsInPeriod = await _chargingModuleApi.GetReservationCountByRangeAsync(normalizedFrom, normalizedTo);
        var companies = await _companiesModuleApi.GetCompaniesForAdminAsync();
        var totalClientUsers = await _userManager.GetUsersInRoleAsync("Customer");

        var model = new AdminDashboardViewModel
        {
            ReservationsInPeriod = reservationsInPeriod,
            TotalCompanies = companies.Count,
            TotalCompanyUsers = companies.Sum(c => c.ActiveMembersCount),
            TotalClientUsers = totalClientUsers.Count,
            FromUtc = normalizedFrom,
            ToUtc = normalizedTo
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
