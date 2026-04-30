using System.Security.Claims;
using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer")]
public class ReservationController : Controller
{
    private readonly IReservationService _reservationService;
    private readonly IPromotionService _promotionService;

    public ReservationController(IReservationService reservationService, IPromotionService promotionService)
    {
        _reservationService = reservationService;
        _promotionService = promotionService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var nowUtc = DateTime.UtcNow;
        var result = await _reservationService.GetUserReservationsAsync(userId.Value);
        var model = new ReservationListViewModel
        {
            Reservations = result.Data?.Select(r => new ReservationListItemViewModel
            {
                Id = r.Id,
                StationName = r.StationName,
                StartTimeUtc = r.StartTimeUtc,
                EndTimeUtc = r.EndTimeUtc,
                EstimatedCost = r.EstimatedCost,
                Status = r.Status,
                CanStart = r.Status == EReservationStatus.Active && r.StartTimeUtc <= nowUtc && nowUtc < r.EndTimeUtc
            }).ToList() ?? new List<ReservationListItemViewModel>()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid stationId, DateTime startTimeUtc, DateTime endTimeUtc, decimal? estimatedEnergyKwh = null, string? promotionCode = null)
    {
        if (stationId == Guid.Empty)
        {
            return RedirectToAction("Index", "Home", new { area = string.Empty });
        }

        var durationMinutes = (int)Math.Ceiling((endTimeUtc - startTimeUtc).TotalMinutes);
        if (durationMinutes <= 0)
        {
            durationMinutes = 60;
            startTimeUtc = DateTime.UtcNow.AddMinutes(30);
            endTimeUtc = startTimeUtc.AddMinutes(durationMinutes);
        }

        var stationResult = await _reservationService.GetStationDetailsAsync(stationId);
        var canReserve = stationResult.Success
                         && stationResult.Data != null
                         && stationResult.Data.Status != EStationStatus.Maintenance;

        var estimateResult = canReserve
            ? await _reservationService.EstimateCostAsync(stationId, durationMinutes, estimatedEnergyKwh)
            : ServiceResult<CostEstimateDto>.Ok(BllDtoFactory.CreateCostEstimateDto(durationMinutes, 0));

        var model = new ReservationCreateViewModel
        {
            StationId = stationId,
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            EstimatedEnergyKwh = estimatedEnergyKwh,
            PromotionCode = promotionCode,
            EstimatedCost = estimateResult.Data?.EstimatedCost ?? 0,
            StationName = stationResult.Data?.Name ?? string.Empty,
            CanReserve = canReserve
        };
        await PopulatePromotionOptionsAsync(model, GetCurrentUserId());

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReservationCreateViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await PopulatePromotionOptionsAsync(model, userId);
            return View(model);
        }

        var result = await _reservationService.ReserveAsync(
            userId.Value,
            BllDtoFactory.CreateReservationCreateDto(
                model.StationId,
                model.StartTimeUtc,
                model.EndTimeUtc,
                model.EstimatedEnergyKwh,
                model.PromotionCode));

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }

            var duration = (int)Math.Ceiling((model.EndTimeUtc - model.StartTimeUtc).TotalMinutes);
            if (duration > 0)
            {
                var estimate = await _reservationService.EstimateCostAsync(model.StationId, duration, model.EstimatedEnergyKwh);
                model.EstimatedCost = estimate.Data?.EstimatedCost ?? model.EstimatedCost;
            }

            await PopulatePromotionOptionsAsync(model, userId);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _reservationService.GetReservationDetailsAsync(id, userId.Value);
        if (!result.Success || result.Data == null)
        {
            return Forbid();
        }

        var model = new ReservationDetailViewModel
        {
            Id = result.Data.Id,
            StationName = result.Data.StationName,
            StartTimeUtc = result.Data.StartTimeUtc,
            EndTimeUtc = result.Data.EndTimeUtc,
            ExpiresAtUtc = result.Data.ExpiresAtUtc,
            CancelledAtUtc = result.Data.CancelledAtUtc,
            EstimatedCost = result.Data.EstimatedCost,
            Status = result.Data.Status
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _reservationService.StartReservationAsync(id, userId.Value);
        if (!result.Success && result.Errors.Any(e => e.Code == "FORBIDDEN"))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _reservationService.CancelReservationAsync(id, userId.Value);
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

    private async Task PopulatePromotionOptionsAsync(ReservationCreateViewModel model, Guid? userId)
    {
        model.AvailablePromotions = new List<PromotionSelectOptionViewModel>();
        if (userId == null)
        {
            return;
        }

        var promotionsResult = await _promotionService.GetUserPromotionsAsync(userId.Value);
        model.AvailablePromotions = promotionsResult.Data?
            .OrderBy(p => p.Code)
            .Select(p => new PromotionSelectOptionViewModel
            {
                Code = p.Code,
                DisplayText = $"{p.Code} (-{p.DiscountValue:0.##}%)"
            })
            .ToList() ?? new List<PromotionSelectOptionViewModel>();
    }
}
