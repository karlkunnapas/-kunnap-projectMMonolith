using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Root.ViewModels;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer")]
public class PromotionController : Controller
{
    private readonly IApiClient _apiClient;

    public PromotionController(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var promotions = await _apiClient.GetAsync<List<UserPromotionResponseDto>>("api/v1/reservation/promotions");
        var model = new PromotionWalletViewModel
        {
            Promotions = promotions
                .Where(p => !p.IsUsed)
                .Select(p => new PromotionWalletItemViewModel
                {
                    Id = p.Id,
                    Code = p.Code,
                    DiscountValue = p.DiscountValue,
                    ValidFromUtc = p.ValidFromUtc,
                    ValidToUtc = p.ValidToUtc,
                    IsActive = p.IsActive,
                    AddedAtUtc = p.ValidFromUtc
                }).ToList()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Redeem(PromotionWalletViewModel model)
    {
        try
        {
            await _apiClient.PostAsync<UserPromotionResponseDto>("api/v1/reservation/promotions/redeem", new RedeemPromotionRequestDto
            {
                Code = model.RedeemCode
            });
            TempData["PromotionSuccess"] = App.Resources.Views.Root.Promotion.Index.RedeemSuccess;
        }
        catch (ApiException ex)
        {
            TempData["PromotionError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id)
    {
        try
        {
            await _apiClient.DeleteAsync($"api/v1/reservation/promotions/{id}");
            TempData["PromotionSuccess"] = "Promotion removed from wallet.";
        }
        catch (ApiException ex)
        {
            TempData["PromotionError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
