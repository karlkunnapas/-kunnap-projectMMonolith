using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAppClient.Helpers;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize]
public abstract class CompanyBaseController : Controller
{
    protected readonly IApiClient ApiClient;

    protected CompanyBaseController(IApiClient apiClient)
    {
        ApiClient = apiClient;
    }

    protected async Task<(Guid CompanyId, string CompanySlug, string Role)?> ResolveCompanyAsync(Guid? requestedCompanyId = null)
    {
        var companiesResponse = await ApiClient.GetAsync<UserCompaniesResponseDto>("api/v1/customeraccount/companies");
        if (companiesResponse.Companies.Count == 0)
        {
            return null;
        }

        UserCompanyItemDto? selected = null;
        if (requestedCompanyId.HasValue)
        {
            selected = companiesResponse.Companies.FirstOrDefault(c => c.CompanyId == requestedCompanyId.Value);
        }

        if (selected == null)
        {
            var sessionCompanyId = HttpContext.Session.GetString("SelectedCompanyId");
            if (Guid.TryParse(sessionCompanyId, out var parsed))
            {
                selected = companiesResponse.Companies.FirstOrDefault(c => c.CompanyId == parsed);
            }
        }

        selected ??= companiesResponse.Companies.First();
        SetSelectedCompany(selected);
        return (selected.CompanyId, selected.CompanySlug, selected.Role);
    }

    protected bool HasManagerAccess(string role)
    {
        return EnumParser.ParseCompanyRole(role) >= WebAppClient.Enums.ECompanyRole.Manager;
    }

    protected bool HasOwnerAccess(string role)
    {
        return EnumParser.ParseCompanyRole(role) == WebAppClient.Enums.ECompanyRole.Owner;
    }

    protected void SetSelectedCompany(UserCompanyItemDto company)
    {
        HttpContext.Session.SetString("SelectedCompanyId", company.CompanyId.ToString());
        HttpContext.Session.SetString("SelectedCompanySlug", company.CompanySlug);
        HttpContext.Session.SetString("SelectedCompanyRole", company.Role);
    }

    protected async Task<Dictionary<Guid, string>> GetLocalizedStationNameMapAsync()
    {
        var stations = await ApiClient.GetAsync<List<StationSummaryDto>>("api/v1/station");
        return stations
            .GroupBy(s => s.Id)
            .ToDictionary(
                g => g.Key,
                g => LocalizationHelper.GetLocalizedName(g.First().Name, g.First().NameTranslations));
    }
}
