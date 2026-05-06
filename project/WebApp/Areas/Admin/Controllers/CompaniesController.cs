using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Admin.ViewModels;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root")]
public class CompaniesController : Controller
{
    private readonly IAdminPanelService _adminPanelService;

    public CompaniesController(IAdminPanelService adminPanelService)
    {
        _adminPanelService = adminPanelService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search = null)
    {
        var result = await _adminPanelService.GetCompaniesAsync(search);
        if (!result.Success || result.Data == null)
        {
            return BadRequest();
        }

        var model = new AdminCompanyListViewModel
        {
            Search = result.Data.Search,
            Items = result.Data.Items.Select(item => new AdminCompanyListItemViewModel
            {
                CompanyId = item.CompanyId,
                Name = item.Name,
                ContactEmail = item.ContactEmail,
                ContactPhone = item.ContactPhone,
                Slug = item.Slug,
                IsActive = item.IsActive,
                ActiveMemberCount = item.ActiveMemberCount
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
        var actor = User.Identity?.Name ?? "admin";
        var result = await _adminPanelService.SetCompanyActivationAsync(companyId, isActive, actor);
        if (!result.Success)
        {
            TempData["AdminCompaniesError"] = result.Errors.FirstOrDefault()?.Message ?? "Operation failed.";
            return RedirectToAction(nameof(Index), new { search });
        }

        TempData["AdminCompaniesSuccess"] = isActive
            ? App.Resources.Views.Admin.Companies.Index.CompanyActivated
            : App.Resources.Views.Admin.Companies.Index.CompanyInactivated;

        return RedirectToAction(nameof(Index), new { search });
    }
}
