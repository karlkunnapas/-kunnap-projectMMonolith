using System.Security.Claims;
using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Areas.Company.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize]
public class PromotionController : Controller
{
    private readonly IPromotionService _promotionService;
    private readonly AppDbContext _context;

    public PromotionController(IPromotionService promotionService, AppDbContext context)
    {
        _promotionService = promotionService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var result = await _promotionService.GetCompanyPromotionsAsync(resolvedCompany.Value);
        if (!result.Success)
        {
            return Forbid();
        }

        var model = new CompanyPromotionListViewModel
        {
            CompanyId = resolvedCompany.Value,
            Promotions = result.Data?.Select(MapItem).ToList() ?? new List<CompanyPromotionItemViewModel>()
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

        var result = await _promotionService.CreateCompanyPromotionAsync(resolvedCompany.Value, new PromotionUpsertDto
        {
            Code = model.Code,
            DiscountValue = model.DiscountValue,
            ValidFromUtc = model.ValidFromUtc,
            ValidToUtc = model.ValidToUtc,
            IsActive = model.IsActive
        });

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }

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

        var result = await _promotionService.GetCompanyPromotionAsync(resolvedCompany.Value, id);
        if (!result.Success || result.Data == null)
        {
            return Forbid();
        }

        return View(new CompanyPromotionFormViewModel
        {
            CompanyId = resolvedCompany.Value,
            Id = result.Data.Id,
            Code = result.Data.Code,
            DiscountValue = result.Data.DiscountValue,
            ValidFromUtc = result.Data.ValidFromUtc,
            ValidToUtc = result.Data.ValidToUtc,
            IsActive = result.Data.IsActive
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

        var result = await _promotionService.UpdateCompanyPromotionAsync(resolvedCompany.Value, id, new PromotionUpsertDto
        {
            Code = model.Code,
            DiscountValue = model.DiscountValue,
            ValidFromUtc = model.ValidFromUtc,
            ValidToUtc = model.ValidToUtc,
            IsActive = model.IsActive
        });

        if (!result.Success)
        {
            if (result.Errors.Any(e => e.Code == "FORBIDDEN"))
            {
                return Forbid();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }

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

        var result = await _promotionService.DeleteCompanyPromotionAsync(resolvedCompany.Value, id);
        if (!result.Success && result.Errors.Any(e => e.Code == "FORBIDDEN"))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new { companyId = resolvedCompany.Value });
    }

    private static CompanyPromotionItemViewModel MapItem(PromotionSummaryDto dto)
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

        return await _context.AppUserCompanies
            .AsNoTracking()
            .Where(uc => uc.AppUserId == userId && uc.IsActive && uc.Role >= ECompanyRole.Manager)
            .OrderByDescending(uc => uc.JoinedAtUtc)
            .Select(uc => uc.CompanyId)
            .ToListAsync();
    }
}
