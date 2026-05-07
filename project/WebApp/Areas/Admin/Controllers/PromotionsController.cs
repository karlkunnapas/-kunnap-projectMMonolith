using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root,SystemAdmin")]
[Route("Admin/[controller]")]
public class PromotionsController(IAdminPanelService adminPanelService) : Controller
{
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(bool showExpired = false)
    {
        var result = await adminPanelService.GetSystemPromotionsAsync();
        if (!result.Success || result.Data == null)
        {
            return RedirectToAction("Dashboard", "Dashboard");
        }

        var items = result.Data.Items.Select(p => new AdminPromotionListItemViewModel
        {
            PromotionId = p.PromotionId,
            Code = p.Code,
            DiscountValue = p.DiscountValue,
            ValidFromUtc = p.ValidFromUtc,
            ValidToUtc = p.ValidToUtc,
            IsActive = p.IsActive,
            CompanyName = p.CompanyName,
            IsSystemLevel = p.IsSystemLevel
        }).ToList();

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

        var userName = User.Identity?.Name ?? "Unknown";
        var dto = new AdminPromotionFormDto
        {
            Code = model.Code,
            DiscountValue = model.DiscountValue,
            ValidFromUtc = model.ValidFromUtc,
            ValidToUtc = model.ValidToUtc,
            IsActive = model.IsActive
        };

        var result = await adminPanelService.CreateSystemPromotionAsync(dto, userName);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Errors.FirstOrDefault()?.Message ?? "Failed to create promotion.");
            return View("Form", model);
        }

        TempData["SuccessMessage"] = "Promotion created successfully.";
        return RedirectToAction("Index");
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var result = await adminPanelService.GetSystemPromotionAsync(id);
        if (!result.Success || result.Data == null)
        {
            return NotFound();
        }

        var model = new AdminPromotionFormViewModel
        {
            PromotionId = result.Data.PromotionId,
            Code = result.Data.Code,
            DiscountValue = result.Data.DiscountValue,
            ValidFromUtc = result.Data.ValidFromUtc,
            ValidToUtc = result.Data.ValidToUtc,
            IsActive = result.Data.IsActive
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

        var userName = User.Identity?.Name ?? "Unknown";
        var dto = new AdminPromotionFormDto
        {
            PromotionId = id,
            Code = model.Code,
            DiscountValue = model.DiscountValue,
            ValidFromUtc = model.ValidFromUtc,
            ValidToUtc = model.ValidToUtc,
            IsActive = model.IsActive
        };

        var result = await adminPanelService.UpdateSystemPromotionAsync(id, dto, userName);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Errors.FirstOrDefault()?.Message ?? "Failed to update promotion.");
            return View("Form", model);
        }

        TempData["SuccessMessage"] = "Promotion updated successfully.";
        return RedirectToAction("Index");
    }

    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userName = User.Identity?.Name ?? "Unknown";
        var result = await adminPanelService.DeleteSystemPromotionAsync(id, userName);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message ?? "Failed to delete promotion.";
            return RedirectToAction("Index");
        }

        TempData["SuccessMessage"] = "Promotion deleted successfully.";
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
    public string CompanyName { get; set; } = "System Level";
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
