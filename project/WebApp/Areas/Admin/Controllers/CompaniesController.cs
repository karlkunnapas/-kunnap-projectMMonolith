using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Companies;
using WebApp.Areas.Admin.ViewModels;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root")]
public class CompaniesController : Controller
{
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public CompaniesController(ICompaniesModuleApi companiesModuleApi)
    {
        _companiesModuleApi = companiesModuleApi;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search = null)
    {
        var companies = await _companiesModuleApi.GetCompaniesForAdminAsync(search);

        var model = new AdminCompanyListViewModel
        {
            Search = search,
            Items = companies.Select(item => new AdminCompanyListItemViewModel
            {
                CompanyId = item.CompanyId,
                Name = item.CompanyName,
                ContactEmail = item.ContactEmail,
                ContactPhone = string.Empty,
                Slug = item.Slug,
                IsActive = item.IsActive,
                ActiveMemberCount = item.ActiveMembersCount
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(Guid companyId, string? search = null)
    {
        return await ChangeActivationState(companyId, true, search);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid companyId, string? search = null)
    {
        return await ChangeActivationState(companyId, false, search);
    }

    private async Task<IActionResult> ChangeActivationState(Guid companyId, bool isActive, string? search)
    {
        var result = await _companiesModuleApi.SetCompanyActivationAsync(companyId, isActive);
        if (result == null)
        {
            TempData["AdminCompaniesError"] = "Operation failed.";
            return RedirectToAction(nameof(Index), new { search });
        }

        TempData["AdminCompaniesSuccess"] = isActive
            ? App.Resources.Views.Admin.Companies.Index.CompanyActivated
            : App.Resources.Views.Admin.Companies.Index.CompanyInactivated;

        return RedirectToAction(nameof(Index), new { search });
    }
}
