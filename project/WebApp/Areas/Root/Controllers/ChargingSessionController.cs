using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer")]
public class ChargingSessionController : Controller
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public ChargingSessionController(
        IChargingModuleApi chargingModuleApi,
        ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
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

        var result = await _chargingModuleApi.GetUserChargingSessionsAsync(userId.Value);
        var model = new ChargingSessionListViewModel
        {
            Sessions = result.Select(MapToDetail).ToList()
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

        var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(reservationId, userId.Value);
        if (reservation == null)
        {
            return Forbid();
        }

        var duration = Math.Max(1, (int)Math.Ceiling((reservation.EndTimeUtc - reservation.StartTimeUtc).TotalMinutes));

        var model = new ChargingSessionStartViewModel
        {
            ReservationId = reservation.Id,
            StationId = reservation.ChargingStationId,
            StationName = reservation.StationName,
            EstimatedCost = reservation.EstimatedCost,
            EstimatedDurationMinutes = duration
        };

        if (reservation.PromotionId is Guid reservationPromotionId)
        {
            var promotions = await _companiesModuleApi.GetUserPromotionsAsync(userId.Value);
            var lockedPromotion = promotions
                .FirstOrDefault(p => p.PromotionId == reservationPromotionId && !p.IsUsed);
            model.PromotionCode = lockedPromotion?.Promotion?.Code;
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

        var reservationId = model.ReservationId ?? Guid.Empty;
        var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(reservationId, userId.Value);
        if (reservation == null)
        {
            return Forbid();
        }
        if (reservation.Status != EReservationStatus.Active)
        {
            ModelState.AddModelError(string.Empty, "Reservation is not active.");
            return View(model);
        }
        if (reservation.StartTimeUtc > DateTime.UtcNow || DateTime.UtcNow >= reservation.EndTimeUtc)
        {
            ModelState.AddModelError(string.Empty, "Reservation cannot be started at this time.");
            return View(model);
        }
        var existingSession = await _chargingModuleApi.GetChargingSessionByReservationIdAsync(reservation.Id);
        if (existingSession != null)
        {
            return RedirectToAction(nameof(Details), new { id = existingSession.Id });
        }

        var created = await _chargingModuleApi.CreateChargingSessionAsync(new ChargingSessionContract
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            ChargingStationId = reservation.ChargingStationId,
            ReservationId = reservation.Id,
            PromotionId = reservation.PromotionId,
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = null,
            EnergyConsumed = 0m,
            Cost = 0m,
            StationName = reservation.StationName
        });
        await _chargingModuleApi.UpdateReservationStatusAsync(
            reservation.Id,
            EReservationStatus.Started,
            reservation.ExpiresAtUtc,
            reservation.CancelledAtUtc,
            EStationStatus.InUse);

        if (created == null)
        {
            ModelState.AddModelError(string.Empty, "Unable to start charging session.");
            return View(model);
        }

        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var sessionContract = await _chargingModuleApi.GetChargingSessionByIdForUserAsync(id, userId.Value);
        if (sessionContract == null)
        {
            return Forbid();
        }

        var model = MapToDetail(sessionContract);
        if (model.IsActive)
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

            Guid? lockedPromotionId = sessionContract.PromotionId;
            if (!lockedPromotionId.HasValue && sessionContract.ReservationId.HasValue)
            {
                var reservationContract = await _chargingModuleApi.GetReservationByIdForUserAsync(sessionContract.ReservationId.Value, userId.Value);
                lockedPromotionId = reservationContract?.PromotionId;
            }

            var userPromotions = await _companiesModuleApi.GetUserPromotionsAsync(userId.Value);
            if (lockedPromotionId.HasValue)
            {
                var lockedPromotion = userPromotions.FirstOrDefault(p => p.PromotionId == lockedPromotionId.Value && !p.IsUsed && p.Promotion != null);
                if (lockedPromotion?.Promotion != null)
                {
                    model.PromotionCode = lockedPromotion.Promotion.Code;
                    model.DiscountPercent = lockedPromotion.Promotion.DiscountValue;
                    model.DiscountAmount = Math.Round(model.BaseCostBeforeDiscount * model.DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
                    model.Cost = Math.Max(0m, model.BaseCostBeforeDiscount - model.DiscountAmount);
                }
            }

            if (string.IsNullOrWhiteSpace(model.PromotionCode))
            {
                model.AvailablePromotions = userPromotions
                    .Where(p => !p.IsUsed && p.Promotion != null)
                    .OrderBy(p => p.Promotion!.Code)
                    .Select(p => new PromotionSelectOptionViewModel
                    {
                        Code = p.Promotion!.Code,
                        DisplayText = $"{p.Promotion.Code} (-{p.Promotion.DiscountValue:0.##}%)"
                    })
                    .ToList();
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

        var session = await _chargingModuleApi.GetChargingSessionByIdForUserAsync(model.Id, userId.Value);
        if (session == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        if (session.EndTimeUtc.HasValue)
        {
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        var reservationPromotionId = session.PromotionId;
        if (!reservationPromotionId.HasValue && session.ReservationId.HasValue)
        {
            var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(session.ReservationId.Value, userId.Value);
            reservationPromotionId = reservation?.PromotionId;
        }

        if (reservationPromotionId.HasValue && !string.IsNullOrWhiteSpace(model.PromotionCode))
        {
            var lockedPromotion = await _companiesModuleApi.GetUserPromotionsAsync(userId.Value);
            var lockCode = lockedPromotion.FirstOrDefault(x => x.PromotionId == reservationPromotionId && !x.IsUsed)?.Promotion?.Code;
            if (!string.Equals(lockCode, model.PromotionCode, StringComparison.OrdinalIgnoreCase))
            {
                TempData["SessionError"] = "Promotion code is locked by reservation and cannot be changed.";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
        }

        Guid? selectedPromotionId = reservationPromotionId;
        decimal discountPercent = 0m;

        if (!selectedPromotionId.HasValue && !string.IsNullOrWhiteSpace(model.PromotionCode))
        {
            var selectedPromotion = await _companiesModuleApi.GetValidUserPromotionByCodeAsync(userId.Value, model.PromotionCode);
            if (selectedPromotion?.Promotion == null || selectedPromotion.IsUsed)
            {
                TempData["SessionError"] = "Invalid promotion code.";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }

            var stationCompanyId = await _chargingModuleApi.GetStationCompanyIdAsync(session.ChargingStationId);
            var promotionCompanyId = selectedPromotion.Promotion.CompanyId;
            if (promotionCompanyId.HasValue && stationCompanyId != promotionCompanyId)
            {
                TempData["SessionError"] = "Promotion is not valid for this charging station company.";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }

            selectedPromotionId = selectedPromotion.PromotionId;
            discountPercent = selectedPromotion.Promotion.DiscountValue;
        }
        else if (selectedPromotionId.HasValue)
        {
            var userPromotions = await _companiesModuleApi.GetUserPromotionsAsync(userId.Value);
            discountPercent = userPromotions.FirstOrDefault(x => x.PromotionId == selectedPromotionId && x.Promotion != null)?.Promotion?.DiscountValue ?? 0m;
        }

        var endTimeUtc = DateTime.UtcNow;
        var durationMinutes = Math.Max(1, (int)Math.Ceiling((endTimeUtc - session.StartTimeUtc).TotalMinutes));
        var energyConsumed = CalculateEnergyEstimateKwh(durationMinutes, session.StationMaxPower);
        var baseCost = Math.Round(energyConsumed * session.StationPricePerKwh, 2, MidpointRounding.AwayFromZero);
        var discountAmount = Math.Round(baseCost * discountPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var totalCost = Math.Max(0m, baseCost - discountAmount);

        var completed = await _chargingModuleApi.CompleteChargingSessionAsync(
            session.Id,
            endTimeUtc,
            energyConsumed,
            totalCost,
            selectedPromotionId,
            EStationStatus.Available);
        if (!completed)
        {
            TempData["SessionError"] = "Unable to stop charging session.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        if (selectedPromotionId.HasValue)
        {
            var promotions = await _companiesModuleApi.GetUserPromotionsAsync(userId.Value);
            var selectedUserPromotion = promotions.FirstOrDefault(p => p.PromotionId == selectedPromotionId.Value && !p.IsUsed);
            if (selectedUserPromotion != null)
            {
                await _companiesModuleApi.RemoveUserPromotionAsync(userId.Value, selectedUserPromotion.Id);
            }
        }

        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    private static ChargingSessionDetailViewModel MapToDetail(ChargingSessionContract session)
    {
        var duration = session.EndTimeUtc.HasValue
            ? Math.Max(1, (int)Math.Ceiling((session.EndTimeUtc.Value - session.StartTimeUtc).TotalMinutes))
            : Math.Max(1, (int)Math.Ceiling((DateTime.UtcNow - session.StartTimeUtc).TotalMinutes));
        var baseCost = session.Cost;
        var discountPercent = session.PromotionDiscountValue ?? 0m;
        if (discountPercent > 0m)
        {
            baseCost = Math.Round(session.Cost / (1m - (discountPercent / 100m)), 2, MidpointRounding.AwayFromZero);
        }

        return new ChargingSessionDetailViewModel
        {
            Id = session.Id,
            StationName = session.StationName,
            ReservationId = session.ReservationId,
            StartTimeUtc = session.StartTimeUtc,
            EndTimeUtc = session.EndTimeUtc,
            DurationMinutes = duration,
            EnergyConsumedKwh = session.EnergyConsumed,
            Cost = session.Cost,
            BaseCostBeforeDiscount = baseCost,
            DiscountPercent = discountPercent,
            DiscountAmount = Math.Max(0m, baseCost - session.Cost),
            PromotionCode = session.PromotionCode,
            IsActive = !session.EndTimeUtc.HasValue
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
