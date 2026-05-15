using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Companies;
using WebApp.Areas.Company.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize]
public class PromotionController : Controller
{
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public PromotionController(ICompaniesModuleApi companiesModuleApi)
    {
        _companiesModuleApi = companiesModuleApi;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var result = await _companiesModuleApi.GetCompanyPromotionsAsync(resolvedCompany.Value);

        var model = new CompanyPromotionListViewModel
        {
            CompanyId = resolvedCompany.Value,
            Promotions = result.Select(MapItem).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        return View(new CompanyPromotionFormViewModel
        {
            CompanyId = resolvedCompany.Value,
            ValidFromUtc = DateTime.UtcNow,
            ValidToUtc = DateTime.UtcNow.AddDays(30),
            IsActive = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompanyPromotionFormViewModel model)
    {
        var resolvedCompany = await ResolveCompanyAsync(model.CompanyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.CompanyId = resolvedCompany.Value;
            return View(model);
        }

        var result = await _companiesModuleApi.CreateCompanyPromotionAsync(
            resolvedCompany.Value,
            new UpsertCompanyPromotionContract
            {
                Code = model.Code,
                DiscountValue = model.DiscountValue,
                ValidFromUtc = model.ValidFromUtc,
                ValidToUtc = model.ValidToUtc,
                IsActive = model.IsActive
            });

        if (!result.Success || result.Promotion == null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to create promotion.");

            model.CompanyId = resolvedCompany.Value;
            return View(model);
        }

        return RedirectToAction(nameof(Index), new { companyId = resolvedCompany.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var result = await _companiesModuleApi.GetCompanyPromotionAsync(resolvedCompany.Value, id);
        if (result == null)
        {
            return Forbid();
        }

        return View(new CompanyPromotionFormViewModel
        {
            CompanyId = resolvedCompany.Value,
            Id = result.Id,
            Code = result.Code,
            DiscountValue = result.DiscountValue,
            ValidFromUtc = result.ValidFromUtc,
            ValidToUtc = result.ValidToUtc,
            IsActive = result.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CompanyPromotionFormViewModel model)
    {
        var resolvedCompany = await ResolveCompanyAsync(model.CompanyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.CompanyId = resolvedCompany.Value;
            model.Id = id;
            return View(model);
        }

        var result = await _companiesModuleApi.UpdateCompanyPromotionAsync(
            resolvedCompany.Value,
            id,
            new UpsertCompanyPromotionContract
            {
                Code = model.Code,
                DiscountValue = model.DiscountValue,
                ValidFromUtc = model.ValidFromUtc,
                ValidToUtc = model.ValidToUtc,
                IsActive = model.IsActive
            });

        if (!result.Success || result.Promotion == null)
        {
            if (result.ErrorCode == "FORBIDDEN")
            {
                return Forbid();
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to update promotion.");

            model.CompanyId = resolvedCompany.Value;
            model.Id = id;
            return View(model);
        }

        return RedirectToAction(nameof(Index), new { companyId = resolvedCompany.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid companyId, Guid id)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var result = await _companiesModuleApi.DeleteCompanyPromotionAsync(resolvedCompany.Value, id);
        if (!result)
        {
            return BadRequest();
        }

        return RedirectToAction(nameof(Index), new { companyId = resolvedCompany.Value });
    }

    private static CompanyPromotionItemViewModel MapItem(CompanyPromotionContract dto)
    {
        return new CompanyPromotionItemViewModel
        {
            Id = dto.Id,
            Code = dto.Code,
            DiscountValue = dto.DiscountValue,
            ValidFromUtc = dto.ValidFromUtc,
            ValidToUtc = dto.ValidToUtc,
            IsActive = dto.IsActive
        };
    }

    private async Task<Guid?> ResolveCompanyAsync(Guid? requestedCompanyId)
    {
        var membershipCompanyIds = await ResolveMembershipCompanyIdsAsync();
        if (membershipCompanyIds.Count == 0)
        {
            return null;
        }

        var resolvedCompanyId = requestedCompanyId ?? membershipCompanyIds[0];
        return membershipCompanyIds.Contains(resolvedCompanyId) ? resolvedCompanyId : null;
    }

    private async Task<List<Guid>> ResolveMembershipCompanyIdsAsync()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return new List<Guid>();
        }

        var memberships = await _companiesModuleApi.GetUserCompaniesAsync(userId);
        return memberships
            .Where(m => HasManagerAccess(m.Role))
            .Select(m => m.CompanyId)
            .ToList();
    }

    private static bool HasManagerAccess(string role)
    {
        return role.Equals("Manager", StringComparison.OrdinalIgnoreCase)
               || role.Equals("Owner", StringComparison.OrdinalIgnoreCase);
    }
}
