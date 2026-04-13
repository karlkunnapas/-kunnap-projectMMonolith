using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Root.ViewModels;
using WebAppClient.Helpers;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize]
public class ChargingSessionController : Controller
{
    private readonly IApiClient _apiClient;

    public ChargingSessionController(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return await History();
    }

    [HttpGet]
    public async Task<IActionResult> History()
    {
        var sessions = await _apiClient.GetAsync<List<SessionResponseDto>>("api/v1/chargingsession");
        var localizedNameByStationId = await GetLocalizedStationNameMapAsync();
        var model = new ChargingSessionListViewModel
        {
            Sessions = sessions.Select(s => MapToDetail(s, localizedNameByStationId.GetValueOrDefault(s.StationId, s.StationName))).ToList()
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Start(Guid reservationId)
    {
        var reservation = await _apiClient.GetAsync<ReservationResponseDto>($"api/v1/reservation/{reservationId}");
        var localizedNameByStationId = await GetLocalizedStationNameMapAsync();
        var duration = Math.Max(1, (int)Math.Ceiling((reservation.EndTimeUtc - reservation.StartTimeUtc).TotalMinutes));
        return View(new ChargingSessionStartViewModel
        {
            ReservationId = reservation.Id,
            StationId = reservation.StationId,
            StationName = localizedNameByStationId.GetValueOrDefault(reservation.StationId, reservation.StationName),
            EstimatedCost = reservation.EstimatedCost,
            EstimatedDurationMinutes = duration
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(ChargingSessionStartViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await _apiClient.PostAsync<SessionResponseDto>("api/v1/chargingsession/start", new SessionStartRequestDto
            {
                StationId = model.StationId,
                ReservationId = model.ReservationId
            });
            return RedirectToAction(nameof(Details), new { id = result.Id });
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        try
        {
            var session = await _apiClient.GetAsync<SessionDetailResponseDto>($"api/v1/chargingsession/{id}");
            var localizedNameByStationId = await GetLocalizedStationNameMapAsync();
            var model = new ChargingSessionDetailViewModel
            {
                Id = session.Id,
                StationName = localizedNameByStationId.GetValueOrDefault(session.StationId, session.StationName),
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

            if (model.IsActive && string.IsNullOrWhiteSpace(model.PromotionCode))
            {
                var promotions = await _apiClient.GetAsync<List<UserPromotionResponseDto>>("api/v1/reservation/promotions");
                model.AvailablePromotions = promotions
                    .Where(p => p.IsActive && !p.IsUsed)
                    .OrderBy(p => p.Code)
                    .Select(p => new PromotionSelectOptionViewModel
                    {
                        Code = p.Code,
                        DisplayText = $"{p.Code} (-{p.DiscountValue:0.##}%)"
                    }).ToList();
            }

            return View(model);
        }
        catch (ApiException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Stop(ChargingSessionStopViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        try
        {
            await _apiClient.PostAsync<SessionResponseDto>($"api/v1/chargingsession/{model.Id}/stop",
                new SessionStopRequestDto { PromotionCode = model.PromotionCode });
        }
        catch (ApiException ex)
        {
            TempData["SessionError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    private ChargingSessionDetailViewModel MapToDetail(SessionResponseDto session, string stationName)
    {
        var duration = session.EndTimeUtc.HasValue
            ? Math.Max(1, (int)Math.Ceiling((session.EndTimeUtc.Value - session.StartTimeUtc).TotalMinutes))
            : Math.Max(1, (int)Math.Ceiling((DateTime.UtcNow - session.StartTimeUtc).TotalMinutes));

        return new ChargingSessionDetailViewModel
        {
            Id = session.Id,
            StationName = stationName,
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

    private async Task<Dictionary<Guid, string>> GetLocalizedStationNameMapAsync()
    {
        var stations = await _apiClient.GetAsync<List<StationSummaryDto>>("api/v1/station");
        return stations.ToDictionary(
            s => s.Id,
            s => LocalizationHelper.GetLocalizedName(s.Name, s.NameTranslations));
    }
}
