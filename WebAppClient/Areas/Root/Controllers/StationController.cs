using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Root.ViewModels;
using WebAppClient.Helpers;
using WebAppClient.Models;
using WebAppClient.Services;
using WebAppClient.Enums;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize]
public class StationController : Controller
{
    private readonly IApiClient _apiClient;

    public StationController(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, DateTime? dateUtc = null)
    {
        try
        {
            var endpoint = dateUtc.HasValue
                ? $"api/v1/station/{id}?dateUtc={Uri.EscapeDataString(dateUtc.Value.ToString("O"))}"
                : $"api/v1/station/{id}";
            var station = await _apiClient.GetAsync<StationDetailsDto>(endpoint);
            var stationSummaries = await _apiClient.GetAsync<List<StationSummaryDto>>("api/v1/station");
            var stationSummary = stationSummaries.FirstOrDefault(s => s.Id == station.Id);
            var localizedStationName = stationSummary == null
                ? station.Name
                : LocalizationHelper.GetLocalizedName(stationSummary.Name, stationSummary.NameTranslations);
            var vehicles = await _apiClient.GetAsync<List<VehicleResponseDto>>("api/v1/vehicle");
            var promotions = await _apiClient.GetAsync<List<UserPromotionResponseDto>>("api/v1/reservation/promotions");

            var selectedVehicle = vehicles.FirstOrDefault(v => v.BatteryCapacity.HasValue && v.BatteryCapacity.Value > 0);
            var selectedConnector = station.Connectors.FirstOrDefault(c => c.AvailableQuantity > 0) ?? station.Connectors.FirstOrDefault();

            var currentBattery = 25;
            var desiredBattery = 80;
            var batteryDelta = Math.Max(0, desiredBattery - currentBattery);
            var energyKwh = selectedVehicle?.BatteryCapacity is > 0
                ? Math.Round(selectedVehicle.BatteryCapacity.Value * batteryDelta / 100m, 2)
                : 0m;

            var chargingPower = Math.Max(1m, Math.Min(station.MaxPower, 200m));
            var durationMinutes = energyKwh <= 0
                ? 60
                : Math.Max(5, (int)Math.Ceiling((double)(energyKwh / chargingPower * 60m)));
            var estimate = await _apiClient.GetAsync<CostEstimateDto>($"api/v1/station/{id}/estimate?durationMinutes={durationMinutes}&estimatedKwh={energyKwh}");

            var model = new StationDetailsViewModel
            {
                Id = station.Id,
                CompanyId = station.CompanyId,
                Name = localizedStationName,
                Location = station.Location,
                Status = EnumParser.ParseStation(station.Status),
                PricePerKwh = station.PricePerKwh,
                MaxPower = station.MaxPower,
                Connectors = station.Connectors.Select(c => new ConnectorDetailViewModel
                {
                    ConnectorId = c.ConnectorId,
                    Name = c.Name,
                    Quantity = c.Quantity,
                    AvailableQuantity = c.AvailableQuantity,
                    Reservations = c.Reservations.Select(r => new ReservedTimeRangeViewModel
                    {
                        StartTimeUtc = r.StartTimeUtc,
                        EndTimeUtc = r.EndTimeUtc
                    }).ToList()
                }).ToList(),
                ExistingReservations = station.ExistingReservations.Select(r => new StationReservationViewModel
                {
                    StartTimeUtc = r.StartTimeUtc,
                    EndTimeUtc = r.EndTimeUtc,
                    Status = EnumParser.ParseReservation(r.Status)
                }).ToList(),
                AvailableSlots = station.AvailableSlots.Select(s => new AvailabilitySlotViewModel
                {
                    StartTimeUtc = s.StartTimeUtc,
                    EndTimeUtc = s.EndTimeUtc,
                    IsAvailable = s.IsAvailable
                }).ToList(),
                Vehicles = vehicles
                    .Where(v => v.BatteryCapacity.HasValue && v.BatteryCapacity.Value > 0)
                    .Select(v => new VehicleOptionViewModel
                    {
                        Id = v.Id,
                        DisplayName = $"{v.Make} {v.Model}",
                        BatteryCapacityKwh = v.BatteryCapacity ?? 0,
                        IsSelected = selectedVehicle?.Id == v.Id
                    }).ToList(),
                SelectedVehicleId = selectedVehicle?.Id,
                SelectedConnectorId = selectedConnector?.ConnectorId,
                CurrentBatteryPercent = currentBattery,
                DesiredBatteryPercent = desiredBattery,
                CalculatedEnergyKwh = energyKwh,
                CalculatedDurationMinutes = durationMinutes,
                CalculatedCost = estimate.EstimatedCost,
                ReservationForm = new ReservationCreateViewModel
                {
                    StationId = station.Id,
                    StationName = localizedStationName,
                    StartTimeUtc = DateTime.UtcNow.AddMinutes(30),
                    EndTimeUtc = DateTime.UtcNow.AddMinutes(30 + durationMinutes),
                    EstimatedEnergyKwh = energyKwh,
                    EstimatedCost = estimate.EstimatedCost,
                    AvailablePromotions = promotions
                        .Where(p => p.IsActive && !p.IsUsed)
                        .OrderBy(p => p.Code)
                        .Select(p => new PromotionSelectOptionViewModel
                        {
                            Code = p.Code,
                            DisplayText = $"{p.Code} (-{p.DiscountValue:0.##}%)"
                        }).ToList(),
                    CanReserve = EnumParser.ParseStation(station.Status) != EStationStatus.Maintenance
                },
                IssueReportForm = new StationIssueReportViewModel { StationId = station.Id }
            };

            return View(model);
        }
        catch (ApiNotFoundException)
        {
            return NotFound();
        }
        catch (ApiException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportIssue([Bind(Prefix = "IssueReportForm")] StationIssueReportViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["IssueReportError"] = App.Resources.Views.Root.Station.Details.ReportIssueValidationError;
            return RedirectToAction(nameof(Details), new { id = model.StationId });
        }

        try
        {
            await _apiClient.PostAsync($"api/v1/station/{model.StationId}/report-issue",
                new ReportIssueRequestDto { IssueDescription = model.IssueDescription });
            TempData["IssueReportSuccess"] = App.Resources.Views.Root.Station.Details.ReportIssueSuccess;
        }
        catch (ApiException ex)
        {
            TempData["IssueReportError"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = model.StationId });
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailableSlots(Guid stationId, DateTime dateUtc, int durationMinutes = 60)
    {
        try
        {
            var result = await _apiClient.GetAsync<List<AvailabilitySlotDto>>(
                $"api/v1/station/{stationId}/slots?dateUtc={Uri.EscapeDataString(dateUtc.ToString("O"))}&durationMinutes={durationMinutes}");
            return Json(result);
        }
        catch (ApiException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> EstimateCost(Guid stationId, int durationMinutes, decimal? estimatedKwh = null)
    {
        try
        {
            var endpoint = $"api/v1/station/{stationId}/estimate?durationMinutes={durationMinutes}";
            if (estimatedKwh.HasValue)
            {
                endpoint += $"&estimatedKwh={estimatedKwh.Value}";
            }
            var result = await _apiClient.GetAsync<CostEstimateDto>(endpoint);
            return Json(result);
        }
        catch (ApiException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
