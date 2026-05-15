using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer")]
public class ReservationController : Controller
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public ReservationController(IChargingModuleApi chargingModuleApi, ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
    }

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var nowUtc = DateTime.UtcNow;
        var result = await _chargingModuleApi.GetUserReservationsAsync(userId.Value);
        var model = new ReservationListViewModel
        {
            Reservations = result
                .Where(r => r.EndTimeUtc > nowUtc)
                .OrderBy(r => r.StartTimeUtc)
                .Select(r => new ReservationListItemViewModel
                {
                    Id = r.Id,
                    StationName = r.StationName,
                    StartTimeUtc = r.StartTimeUtc,
                    EndTimeUtc = r.EndTimeUtc,
                    EstimatedCost = r.EstimatedCost,
                    Status = r.Status,
                    CanStart = r.Status == Shared.Contracts.Charging.EReservationStatus.Active && r.StartTimeUtc <= nowUtc && nowUtc < r.EndTimeUtc
                }).ToList()
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

        var station = await _chargingModuleApi.GetStationByIdAsync(stationId);
        var canReserve = station != null && station.Status != Shared.Contracts.Charging.EStationStatus.Maintenance;
        var estimateCost = canReserve
            ? CalculateEstimatedCost(station!, durationMinutes, estimatedEnergyKwh)
            : 0m;

        var model = new ReservationCreateViewModel
        {
            StationId = stationId,
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            EstimatedEnergyKwh = estimatedEnergyKwh,
            PromotionCode = promotionCode,
            EstimatedCost = estimateCost,
            StationName = station?.Name ?? string.Empty,
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

        var station = await _chargingModuleApi.GetStationByIdAsync(model.StationId);
        if (station == null || station.Status == Shared.Contracts.Charging.EStationStatus.Maintenance)
        {
            ModelState.AddModelError(string.Empty, "Station is unavailable for reservation.");
            await PopulatePromotionOptionsAsync(model, userId);
            return View(model);
        }

        var overlaps = await _chargingModuleApi.GetOverlappingReservationsAsync(
            model.StationId,
            model.StartTimeUtc,
            model.EndTimeUtc);
        if (overlaps.Any(x => x.Status is Shared.Contracts.Charging.EReservationStatus.Active or Shared.Contracts.Charging.EReservationStatus.Started))
        {
            ModelState.AddModelError(string.Empty, "Selected time slot is no longer available.");
            await PopulatePromotionOptionsAsync(model, userId);
            return View(model);
        }

        Guid? promotionId = null;
        if (!string.IsNullOrWhiteSpace(model.PromotionCode))
        {
            var normalizedCode = model.PromotionCode.Trim();
            var userPromotion = await _companiesModuleApi.GetValidUserPromotionByCodeAsync(userId.Value, normalizedCode);
            if (userPromotion == null || userPromotion.IsUsed || userPromotion.Promotion == null)
            {
                ModelState.AddModelError(nameof(model.PromotionCode), "Selected promotion is invalid or expired.");
                await PopulatePromotionOptionsAsync(model, userId);
                return View(model);
            }

            if (userPromotion.Promotion.CompanyId.HasValue && station.CompanyId != userPromotion.Promotion.CompanyId)
            {
                ModelState.AddModelError(nameof(model.PromotionCode), "Selected promotion is not valid for this station company.");
                await PopulatePromotionOptionsAsync(model, userId);
                return View(model);
            }

            model.PromotionCode = normalizedCode;
            promotionId = userPromotion.PromotionId;
        }

        var created = await _chargingModuleApi.CreateReservationAsync(new ReservationContract
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            ChargingStationId = model.StationId,
            StartTimeUtc = model.StartTimeUtc,
            EndTimeUtc = model.EndTimeUtc,
            ExpiresAtUtc = model.EndTimeUtc,
            CancelledAtUtc = null,
            EstimatedCost = CalculateEstimatedCost(station, (int)Math.Ceiling((model.EndTimeUtc - model.StartTimeUtc).TotalMinutes), model.EstimatedEnergyKwh),
            Status = Shared.Contracts.Charging.EReservationStatus.Active,
            PromotionId = promotionId,
            StationName = station.Name
        });

        if (created == null)
        {
            model.EstimatedCost = CalculateEstimatedCost(station, (int)Math.Ceiling((model.EndTimeUtc - model.StartTimeUtc).TotalMinutes), model.EstimatedEnergyKwh);
            ModelState.AddModelError(string.Empty, "Unable to create reservation.");
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

        var result = await _chargingModuleApi.GetReservationByIdForUserAsync(id, userId.Value);
        if (result == null)
        {
            return Forbid();
        }

        var model = new ReservationDetailViewModel
        {
            Id = result.Id,
            StationName = result.StationName,
            StartTimeUtc = result.StartTimeUtc,
            EndTimeUtc = result.EndTimeUtc,
            ExpiresAtUtc = result.ExpiresAtUtc,
            CancelledAtUtc = result.CancelledAtUtc,
            EstimatedCost = result.EstimatedCost,
            Status = result.Status
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

        var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(id, userId.Value);
        if (reservation == null)
        {
            return Forbid();
        }

        await _chargingModuleApi.UpdateReservationStatusAsync(
            id,
            Shared.Contracts.Charging.EReservationStatus.Started,
            reservation.ExpiresAtUtc,
            reservation.CancelledAtUtc,
            Shared.Contracts.Charging.EStationStatus.InUse);

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

        var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(id, userId.Value);
        if (reservation == null)
        {
            return Forbid();
        }

        await _chargingModuleApi.UpdateReservationStatusAsync(
            id,
            Shared.Contracts.Charging.EReservationStatus.Cancelled,
            reservation.ExpiresAtUtc,
            DateTime.UtcNow,
            Shared.Contracts.Charging.EStationStatus.Available);

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

        var promotions = await _companiesModuleApi.GetUserPromotionsAsync(userId.Value);
        var nowUtc = DateTime.UtcNow;
        model.AvailablePromotions = promotions
            .Where(p =>
                !p.IsUsed
                && p.Promotion != null
                && p.Promotion.IsActive
                && p.Promotion.ValidFromUtc <= nowUtc
                && p.Promotion.ValidToUtc >= nowUtc)
            .OrderBy(p => p.Promotion!.Code)
            .Select(p => new PromotionSelectOptionViewModel
            {
                Code = p.Promotion!.Code,
                DisplayText = $"{p.Promotion.Code} (-{p.Promotion.DiscountValue:0.##}%)"
            })
            .ToList();
    }

    private static decimal CalculateEstimatedCost(ChargingStationContract station, int durationMinutes, decimal? estimatedEnergyKwh)
    {
        if (durationMinutes <= 0)
        {
            return 0m;
        }

        var estimatedEnergy = estimatedEnergyKwh
            ?? Math.Round((durationMinutes / 60m) * station.MaxPower * 0.6m, 2, MidpointRounding.AwayFromZero);

        if (estimatedEnergy < 0)
        {
            estimatedEnergy = 0;
        }

        return Math.Round(estimatedEnergy * station.PricePerKwh, 2, MidpointRounding.AwayFromZero);
    }
}
