using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Company.ViewModels;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Areas.Company.Controllers;

public class PromotionController : CompanyBaseController
{
    public PromotionController(IApiClient apiClient) : base(apiClient)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var promotions = await ApiClient.GetAsync<List<PromotionResponseDto>>($"api/v1/company/{company.Value.CompanyId}/promotion");
        return View(new CompanyPromotionListViewModel
        {
            CompanyId = company.Value.CompanyId,
            Promotions = promotions.Select(MapPromotion).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        return View(new CompanyPromotionFormViewModel
        {
            CompanyId = company.Value.CompanyId,
            ValidFromUtc = DateTime.UtcNow,
            ValidToUtc = DateTime.UtcNow.AddDays(30),
            IsActive = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompanyPromotionFormViewModel model)
    {
        var company = await ResolveCompanyAsync(model.CompanyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await ApiClient.PostAsync<PromotionResponseDto>(
                $"api/v1/company/{company.Value.CompanyId}/promotion",
                new PromotionUpsertRequestDto
                {
                    Code = model.Code,
                    DiscountValue = model.DiscountValue,
                    ValidFromUtc = model.ValidFromUtc,
                    ValidToUtc = model.ValidToUtc,
                    IsActive = model.IsActive
                });
            return RedirectToAction(nameof(Index), new { companyId = company.Value.CompanyId });
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var promotion = await ApiClient.GetAsync<PromotionResponseDto>($"api/v1/company/{company.Value.CompanyId}/promotion/{id}");
        return View(new CompanyPromotionFormViewModel
        {
            CompanyId = company.Value.CompanyId,
            Id = promotion.Id,
            Code = promotion.Code,
            DiscountValue = promotion.DiscountValue,
            ValidFromUtc = promotion.ValidFromUtc,
            ValidToUtc = promotion.ValidToUtc,
            IsActive = promotion.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CompanyPromotionFormViewModel model)
    {
        var company = await ResolveCompanyAsync(model.CompanyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await ApiClient.PutAsync<PromotionResponseDto>(
                $"api/v1/company/{company.Value.CompanyId}/promotion/{id}",
                new PromotionUpsertRequestDto
                {
                    Code = model.Code,
                    DiscountValue = model.DiscountValue,
                    ValidFromUtc = model.ValidFromUtc,
                    ValidToUtc = model.ValidToUtc,
                    IsActive = model.IsActive
                });
            return RedirectToAction(nameof(Index), new { companyId = company.Value.CompanyId });
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid companyId, Guid id)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        await ApiClient.DeleteAsync($"api/v1/company/{company.Value.CompanyId}/promotion/{id}");
        return RedirectToAction(nameof(Index), new { companyId = company.Value.CompanyId });
    }

    private static CompanyPromotionItemViewModel MapPromotion(PromotionResponseDto dto)
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
}
