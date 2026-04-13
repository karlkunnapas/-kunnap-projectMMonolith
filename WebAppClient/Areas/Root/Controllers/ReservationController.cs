using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using WebApp.Areas.Root.ViewModels;
using WebAppClient.Helpers;
using WebAppClient.Models;
using WebAppClient.Services;
using WebAppClient.Enums;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize]
public class ReservationController : Controller
{
    private readonly IApiClient _apiClient;

    public ReservationController(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var reservations = await _apiClient.GetAsync<List<ReservationResponseDto>>("api/v1/reservation");
        var localizedNameByStationId = await GetLocalizedStationNameMapAsync();
        var nowUtc = DateTime.UtcNow;
        var model = new ReservationListViewModel
        {
            Reservations = reservations.Select(r =>
            {
                var status = EnumParser.ParseReservation(r.Status);
                return new ReservationListItemViewModel
                {
                    Id = r.Id,
                    StationName = localizedNameByStationId.GetValueOrDefault(r.StationId, r.StationName),
                    StartTimeUtc = r.StartTimeUtc,
                    EndTimeUtc = r.EndTimeUtc,
                    Status = status,
                    EstimatedCost = r.EstimatedCost,
                    CanStart = status == EReservationStatus.Active && r.StartTimeUtc <= nowUtc && nowUtc < r.EndTimeUtc
                };
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

        var station = await _apiClient.GetAsync<StationDetailsDto>($"api/v1/station/{stationId}");
        var localizedNameByStationId = await GetLocalizedStationNameMapAsync();
        var estimate = await _apiClient.GetAsync<CostEstimateDto>($"api/v1/station/{stationId}/estimate?durationMinutes={durationMinutes}&estimatedKwh={estimatedEnergyKwh ?? 0}");
        var promotions = await _apiClient.GetAsync<List<UserPromotionResponseDto>>("api/v1/reservation/promotions");

        var model = new ReservationCreateViewModel
        {
            StationId = stationId,
            StationName = localizedNameByStationId.GetValueOrDefault(stationId, station.Name),
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            EstimatedEnergyKwh = estimatedEnergyKwh,
            PromotionCode = promotionCode,
            EstimatedCost = estimate.EstimatedCost,
            CanReserve = EnumParser.ParseStation(station.Status) != EStationStatus.Maintenance,
            AvailablePromotions = promotions
                .Where(p => p.IsActive && !p.IsUsed)
                .OrderBy(p => p.Code)
                .Select(p => new PromotionSelectOptionViewModel
                {
                    Code = p.Code,
                    DisplayText = $"{p.Code} (-{p.DiscountValue:0.##}%)"
                }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReservationCreateViewModel model)
    {
        NormalizeEstimatedEnergyKwh(model);

        if (!ModelState.IsValid)
        {
            await LoadPromotionsAsync(model);
            return View(model);
        }

        try
        {
            await _apiClient.PostAsync<ReservationResponseDto>("api/v1/reservation", new ReservationCreateRequestDto
            {
                StationId = model.StationId,
                StartTimeUtc = model.StartTimeUtc,
                EndTimeUtc = model.EndTimeUtc,
                EstimatedEnergyKwh = model.EstimatedEnergyKwh,
                PromotionCode = model.PromotionCode
            });
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadPromotionsAsync(model);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        try
        {
            var reservation = await _apiClient.GetAsync<ReservationResponseDto>($"api/v1/reservation/{id}");
            var localizedNameByStationId = await GetLocalizedStationNameMapAsync();
            return View(new ReservationDetailViewModel
            {
                Id = reservation.Id,
                StationName = localizedNameByStationId.GetValueOrDefault(reservation.StationId, reservation.StationName),
                StartTimeUtc = reservation.StartTimeUtc,
                EndTimeUtc = reservation.EndTimeUtc,
                ExpiresAtUtc = reservation.ExpiresAtUtc,
                CancelledAtUtc = reservation.CancelledAtUtc,
                Status = EnumParser.ParseReservation(reservation.Status),
                EstimatedCost = reservation.EstimatedCost
            });
        }
        catch (ApiException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(Guid id)
    {
        try
        {
            await _apiClient.PostAsync($"api/v1/reservation/{id}/start");
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try
        {
            await _apiClient.PostAsync($"api/v1/reservation/{id}/cancel");
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException)
        {
            return Forbid();
        }
    }

    private async Task LoadPromotionsAsync(ReservationCreateViewModel model)
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

    private void NormalizeEstimatedEnergyKwh(ReservationCreateViewModel model)
    {
        var field = nameof(ReservationCreateViewModel.EstimatedEnergyKwh);
        if (!ModelState.TryGetValue(field, out var state) || state.Errors.Count == 0)
        {
            return;
        }

        var rawValue = state.AttemptedValue;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return;
        }

        model.EstimatedEnergyKwh = parsed;
        ModelState.Remove(field);
    }

    private async Task<Dictionary<Guid, string>> GetLocalizedStationNameMapAsync()
    {
        var stations = await _apiClient.GetAsync<List<StationSummaryDto>>("api/v1/station");
        return stations.ToDictionary(
            s => s.Id,
            s => LocalizationHelper.GetLocalizedName(s.Name, s.NameTranslations));
    }
}
