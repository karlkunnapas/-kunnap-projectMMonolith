using System.Security.Claims;
using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer")]
public class ChargingSessionController : Controller
{
    private readonly IChargingSessionService _chargingSessionService;
    private readonly IReservationService _reservationService;
    private readonly IPromotionService _promotionService;
    private readonly IChargingModuleApi _chargingModuleApi;

    public ChargingSessionController(
        IChargingSessionService chargingSessionService,
        IReservationService reservationService,
        IPromotionService promotionService,
        IChargingModuleApi chargingModuleApi)
    {
        _chargingSessionService = chargingSessionService;
        _reservationService = reservationService;
        _promotionService = promotionService;
        _chargingModuleApi = chargingModuleApi;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return await History();
    }

    [HttpGet]
    public async Task<IActionResult> History()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _chargingSessionService.GetUserSessionsAsync(userId.Value);
        var model = new ChargingSessionListViewModel
        {
            Sessions = result.Data?.Select(MapToDetail).ToList() ?? new List<ChargingSessionDetailViewModel>()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Start(Guid reservationId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var reservationResult = await _reservationService.GetReservationDetailsAsync(reservationId, userId.Value);
        if (!reservationResult.Success || reservationResult.Data == null)
        {
            return Forbid();
        }

        var duration = Math.Max(1, (int)Math.Ceiling((reservationResult.Data.EndTimeUtc - reservationResult.Data.StartTimeUtc).TotalMinutes));

        var model = new ChargingSessionStartViewModel
        {
            ReservationId = reservationResult.Data.Id,
            StationId = reservationResult.Data.StationId,
            StationName = reservationResult.Data.StationName,
            EstimatedCost = reservationResult.Data.EstimatedCost,
            EstimatedDurationMinutes = duration
        };

        var reservationContract = await _chargingModuleApi.GetReservationByIdForUserAsync(reservationId, userId.Value);
        if (reservationContract?.PromotionId is Guid reservationPromotionId)
        {
            var promotionsResult = await _promotionService.GetUserPromotionsAsync(userId.Value);
            var lockedPromotion = promotionsResult.Data?
                .FirstOrDefault(p => p.PromotionId == reservationPromotionId && !p.IsUsed);
            model.PromotionCode = lockedPromotion?.Code;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(ChargingSessionStartViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _chargingSessionService.StartSessionAsync(
            userId.Value,
            BllDtoFactory.CreateChargingSessionStartRequestDto(model.StationId, model.ReservationId));

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

            return View(model);
        }

        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _chargingSessionService.GetSessionDetailsAsync(id, userId.Value);
        if (!result.Success || result.Data == null)
        {
            return Forbid();
        }

        var model = MapToDetail(result.Data);
        if (model.IsActive)
        {
            var sessionContract = await _chargingModuleApi.GetChargingSessionByIdForUserAsync(id, userId.Value);
            if (sessionContract != null)
            {
                var durationMinutes = Math.Max(1, (int)Math.Ceiling((DateTime.UtcNow - sessionContract.StartTimeUtc).TotalMinutes));
                var estimatedEnergyKwh = CalculateEnergyEstimateKwh(durationMinutes, sessionContract.StationMaxPower);

                if (model.EnergyConsumedKwh <= 0)
                {
                    model.DurationMinutes = durationMinutes;
                    model.EnergyConsumedKwh = estimatedEnergyKwh;
                }

                if (model.Cost <= 0)
                {
                    var estimatedCost = Math.Round(sessionContract.StationPricePerKwh * estimatedEnergyKwh, 2, MidpointRounding.AwayFromZero);
                    model.BaseCostBeforeDiscount = estimatedCost;
                    model.Cost = estimatedCost;
                }

                if (string.IsNullOrWhiteSpace(model.PromotionCode))
                {
                    Guid? lockedPromotionId = sessionContract.PromotionId;
                    if (!lockedPromotionId.HasValue && sessionContract.ReservationId.HasValue)
                    {
                        var reservationContract = await _chargingModuleApi.GetReservationByIdForUserAsync(sessionContract.ReservationId.Value, userId.Value);
                        lockedPromotionId = reservationContract?.PromotionId;
                    }

                    var promotionsResult = await _promotionService.GetUserPromotionsAsync(userId.Value);
                    var userPromotions = promotionsResult.Data ?? new List<UserPromotionDto>();

                    if (lockedPromotionId.HasValue)
                    {
                        var lockedPromotion = userPromotions.FirstOrDefault(p => p.PromotionId == lockedPromotionId.Value && !p.IsUsed);
                        if (lockedPromotion != null)
                        {
                            model.PromotionCode = lockedPromotion.Code;
                            model.DiscountPercent = lockedPromotion.DiscountValue;
                            model.DiscountAmount = Math.Round(model.BaseCostBeforeDiscount * model.DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
                            model.Cost = Math.Max(0m, model.BaseCostBeforeDiscount - model.DiscountAmount);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(model.PromotionCode))
                    {
                        model.AvailablePromotions = userPromotions
                            .OrderBy(p => p.Code)
                            .Select(p => new PromotionSelectOptionViewModel
                            {
                                Code = p.Code,
                                DisplayText = $"{p.Code} (-{p.DiscountValue:0.##}%)"
                            })
                            .ToList();
                    }
                }
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Stop(ChargingSessionStopViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var result = await _chargingSessionService.StopSessionAsync(
            userId.Value,
            model.Id,
            BllDtoFactory.CreateChargingSessionStopRequestDto(model.PromotionCode));
        if (!result.Success)
        {
            if (result.Errors.Any(e => e.Code == "FORBIDDEN"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            TempData["SessionError"] = string.Join("; ", result.Errors.Select(e => e.Message));
        }

        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    private static ChargingSessionDetailViewModel MapToDetail(ChargingSessionDto session)
    {
        var duration = session.EndTimeUtc.HasValue
            ? Math.Max(1, (int)Math.Ceiling((session.EndTimeUtc.Value - session.StartTimeUtc).TotalMinutes))
            : Math.Max(1, (int)Math.Ceiling((DateTime.UtcNow - session.StartTimeUtc).TotalMinutes));

        return new ChargingSessionDetailViewModel
        {
            Id = session.Id,
            StationName = session.StationName,
            ReservationId = session.ReservationId,
            StartTimeUtc = session.StartTimeUtc,
            EndTimeUtc = session.EndTimeUtc,
            DurationMinutes = duration,
            EnergyConsumedKwh = session.EnergyConsumedKwh,
            Cost = session.Cost,
            BaseCostBeforeDiscount = session.BaseCostBeforeDiscount,
            DiscountPercent = session.DiscountPercent,
            DiscountAmount = session.DiscountAmount,
            PromotionCode = session.PromotionCode,
            IsActive = session.IsActive
        };
    }

    private static ChargingSessionDetailViewModel MapToDetail(ChargingSessionDetailsDto session)
    {
        return new ChargingSessionDetailViewModel
        {
            Id = session.Id,
            StationName = session.StationName,
            ReservationId = session.ReservationId,
            StartTimeUtc = session.StartTimeUtc,
            EndTimeUtc = session.EndTimeUtc,
            DurationMinutes = session.DurationMinutes,
            EnergyConsumedKwh = session.EnergyConsumedKwh,
            Cost = session.Cost,
            BaseCostBeforeDiscount = session.BaseCostBeforeDiscount,
            DiscountPercent = session.DiscountPercent,
            DiscountAmount = session.DiscountAmount,
            PromotionCode = session.PromotionCode,
            IsActive = session.IsActive
        };
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static decimal CalculateEnergyEstimateKwh(int durationMinutes, decimal? stationMaxPower)
    {
        var effectivePower = Math.Max(1m, Math.Min(stationMaxPower ?? 50m, 200m));
        var durationHours = durationMinutes / 60m;
        return Math.Round(durationHours * effectivePower, 2, MidpointRounding.AwayFromZero);
    }
}
