using System.Security.Claims;
using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer")]
public class PromotionController : Controller
{
    private readonly IPromotionService _promotionService;

    public PromotionController(IPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _promotionService.GetUserPromotionsAsync(userId.Value);
        var model = new PromotionWalletViewModel
        {
            Promotions = result.Data?
                .Where(p => !p.IsUsed)
                .Select(p => new PromotionWalletItemViewModel
            {
                Id = p.Id,
                Code = p.Code,
                DiscountValue = p.DiscountValue,
                ValidFromUtc = p.ValidFromUtc,
                ValidToUtc = p.ValidToUtc,
                AddedAtUtc = p.AddedAtUtc,
                IsActive = p.IsActive
            }).ToList() ?? new List<PromotionWalletItemViewModel>()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Redeem(PromotionWalletViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _promotionService.RedeemPromotionAsync(userId.Value, model.RedeemCode);
        if (!result.Success)
        {
            TempData["PromotionError"] = string.Join("; ", result.Errors.Select(e => e.Message));
        }
        else
        {
            TempData["PromotionSuccess"] = App.Resources.Views.Root.Promotion.Index.RedeemSuccess;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _promotionService.RemoveUserPromotionAsync(userId.Value, id);
        if (!result.Success && result.Errors.Any(e => e.Code == "FORBIDDEN"))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index));
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
