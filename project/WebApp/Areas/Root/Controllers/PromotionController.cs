using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Companies;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer")]
public class PromotionController : Controller
{
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public PromotionController(ICompaniesModuleApi companiesModuleApi)
    {
        _companiesModuleApi = companiesModuleApi;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _companiesModuleApi.GetUserPromotionsAsync(userId.Value);
        var model = new PromotionWalletViewModel
        {
            Promotions = result
                .Where(p => !p.IsUsed && p.Promotion != null)
                .Select(p => new PromotionWalletItemViewModel
            {
                Id = p.Id,
                Code = p.Promotion!.Code,
                DiscountValue = p.Promotion.DiscountValue,
                ValidFromUtc = p.Promotion.ValidFromUtc,
                ValidToUtc = p.Promotion.ValidToUtc,
                AddedAtUtc = p.AddedAtUtc,
                IsActive = p.Promotion.IsActive
            }).ToList()
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

        var result = await _companiesModuleApi.RedeemPromotionAsync(userId.Value, model.RedeemCode);
        if (!result.Success)
        {
            TempData["PromotionError"] = result.ErrorMessage ?? "Unable to redeem promotion.";
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

        var result = await _companiesModuleApi.RemoveUserPromotionAsync(userId.Value, id);
        if (!result)
        {
            return BadRequest();
        }

        return RedirectToAction(nameof(Index));
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
