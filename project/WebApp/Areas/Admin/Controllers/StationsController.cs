using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using System.Globalization;
using WebApp.Areas.Admin.ViewModels;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root")]
public class StationsController : Controller
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public StationsController(IChargingModuleApi chargingModuleApi, ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search = null)
    {
        var stations = await _chargingModuleApi.GetStationsForAdminAsync();
        var companies = await _companiesModuleApi.GetCompaniesForAdminAsync();
        var companyNamesById = companies.ToDictionary(x => x.CompanyId, x => x.CompanyName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            stations = stations.Where(s =>
                s.NameEn.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.NameEt.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Location.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (s.CompanyId.HasValue && companyNamesById.TryGetValue(s.CompanyId.Value, out var companyName) &&
                 companyName.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var model = new AdminStationListViewModel
        {
            Search = search,
            Items = stations.Select(item => new AdminStationListItemViewModel
            {
                StationId = item.StationId,
                Name = ResolveLocalizedName(item.NameEn, item.NameEt),
                Location = item.Location,
                CompanyName = item.CompanyId.HasValue && companyNamesById.TryGetValue(item.CompanyId.Value, out var companyName)
                    ? companyName
                    : string.Empty,
                Status = item.Status.ToString(),
                IsActive = item.IsActive,
                PricePerKwh = item.PricePerKwh,
                MaxPower = item.MaxPower
            }).ToList()
        };

        return View(model);
    }

    private static string ResolveLocalizedName(string nameEn, string nameEt)
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (string.Equals(culture, "et", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(nameEt) ? nameEn : nameEt;
        }

        return string.IsNullOrWhiteSpace(nameEn) ? nameEt : nameEn;
    }
}
