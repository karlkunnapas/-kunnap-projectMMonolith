using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Companies;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root,SystemAdmin")]
[Route("Admin/[controller]")]
public class PromotionsController(ICompaniesModuleApi companiesModuleApi) : Controller
{
    private static string R(string key) =>
        App.Resources.Views.Admin.Promotions.Index.ResourceManager.GetString(key) ?? key;

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(bool showExpired = false)
    {
        var systemItems = (await companiesModuleApi.GetSystemPromotionsAsync())
            .Select(p => new AdminPromotionListItemViewModel
        {
            PromotionId = p.Id,
            Code = p.Code,
            DiscountValue = p.DiscountValue,
            ValidFromUtc = p.ValidFromUtc,
            ValidToUtc = p.ValidToUtc,
            IsActive = p.IsActive,
            CompanyName = R("SystemLevel"),
            IsSystemLevel = true
        }).ToList();
        var companyItems = new List<AdminPromotionListItemViewModel>();
        var companies = await companiesModuleApi.GetCompaniesForAdminAsync();
        foreach (var company in companies)
        {
            var companyPromotions = await companiesModuleApi.GetCompanyPromotionsAsync(company.CompanyId);
            companyItems.AddRange(companyPromotions.Select(p => new AdminPromotionListItemViewModel
            {
                PromotionId = p.Id,
                Code = p.Code,
                DiscountValue = p.DiscountValue,
                ValidFromUtc = p.ValidFromUtc,
                ValidToUtc = p.ValidToUtc,
                IsActive = p.IsActive,
                CompanyName = company.CompanyName,
                IsSystemLevel = false
            }));
        }
        var items = systemItems.Concat(companyItems).ToList();

        if (!showExpired)
        {
            var utcNow = DateTime.UtcNow;
            items = items.Where(i => i.ValidToUtc >= utcNow).ToList();
        }

        var model = new AdminPromotionIndexViewModel
        {
            ShowExpired = showExpired,
            SystemPromotions = items.Where(i => i.IsSystemLevel).ToList(),
            CompanyPromotions = items.Where(i => !i.IsSystemLevel).ToList()
        };

        return View(model);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        var model = new AdminPromotionFormViewModel();
        return View("Form", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] AdminPromotionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        if (string.IsNullOrWhiteSpace(model.Code))
        {
            ModelState.AddModelError("", R("PromotionCodeRequired"));
            return View("Form", model);
        }
        if (model.DiscountValue <= 0)
        {
            ModelState.AddModelError("", R("DiscountValuePositive"));
            return View("Form", model);
        }
        if (model.ValidFromUtc >= model.ValidToUtc)
        {
            ModelState.AddModelError("", R("ValidFromBeforeValidTo"));
            return View("Form", model);
        }

        var dto = new UpsertCompanyPromotionContract
        {
            Code = model.Code,
            DiscountValue = model.DiscountValue,
            ValidFromUtc = model.ValidFromUtc,
            ValidToUtc = model.ValidToUtc,
            IsActive = model.IsActive
        };

        var result = await companiesModuleApi.CreateSystemPromotionAsync(dto);
        if (!result.Success || result.Promotion == null)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? R("CreateFailed"));
            return View("Form", model);
        }

        TempData["SuccessMessage"] = R("CreatedSuccess");
        return RedirectToAction("Index");
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var promotion = await companiesModuleApi.GetSystemPromotionAsync(id);
        if (promotion == null)
        {
            return NotFound();
        }

        var model = new AdminPromotionFormViewModel
        {
            PromotionId = promotion.Id,
            Code = promotion.Code,
            DiscountValue = promotion.DiscountValue,
            ValidFromUtc = promotion.ValidFromUtc,
            ValidToUtc = promotion.ValidToUtc,
            IsActive = promotion.IsActive
        };

        return View("Form", model);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] AdminPromotionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        if (string.IsNullOrWhiteSpace(model.Code))
        {
            ModelState.AddModelError("", R("PromotionCodeRequired"));
            return View("Form", model);
        }
        if (model.DiscountValue <= 0)
        {
            ModelState.AddModelError("", R("DiscountValuePositive"));
            return View("Form", model);
        }
        if (model.ValidFromUtc >= model.ValidToUtc)
        {
            ModelState.AddModelError("", R("ValidFromBeforeValidTo"));
            return View("Form", model);
        }

        var dto = new UpsertCompanyPromotionContract
        {
            Code = model.Code,
            DiscountValue = model.DiscountValue,
            ValidFromUtc = model.ValidFromUtc,
            ValidToUtc = model.ValidToUtc,
            IsActive = model.IsActive
        };

        var result = await companiesModuleApi.UpdateSystemPromotionAsync(id, dto);
        if (!result.Success || result.Promotion == null)
        {
            ModelState.AddModelError("", result.ErrorMessage ?? R("UpdateFailed"));
            return View("Form", model);
        }

        TempData["SuccessMessage"] = R("UpdatedSuccess");
        return RedirectToAction("Index");
    }

    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await companiesModuleApi.DeleteSystemPromotionAsync(id);
        if (!deleted)
        {
            TempData["ErrorMessage"] = R("DeleteFailed");
            return RedirectToAction("Index");
        }

        TempData["SuccessMessage"] = R("DeletedSuccess");
        return RedirectToAction("Index");
    }
}

public class AdminPromotionIndexViewModel
{
    public bool ShowExpired { get; set; }
    public List<AdminPromotionListItemViewModel> SystemPromotions { get; set; } = new();
    public List<AdminPromotionListItemViewModel> CompanyPromotions { get; set; } = new();
}

public class AdminPromotionListItemViewModel
{
    public Guid PromotionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
    public string CompanyName { get; set; } = App.Resources.Views.Admin.Promotions.Index.ResourceManager.GetString("SystemLevel") ?? "System Level";
    public bool IsSystemLevel { get; set; }
}

public class AdminPromotionFormViewModel
{
    public Guid? PromotionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime ValidToUtc { get; set; } = DateTime.UtcNow.AddDays(30);
    public bool IsActive { get; set; } = true;
}
