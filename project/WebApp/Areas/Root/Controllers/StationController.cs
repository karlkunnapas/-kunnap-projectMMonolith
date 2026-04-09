using App.BLL.Services.Interfaces;
using App.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer,CompanyOwner")]
public class StationController : Controller
{
    private readonly IReservationService _reservationService;
    private readonly IAvailabilityService _availabilityService;
    private readonly IVehicleService _vehicleService;
    private readonly IPromotionService _promotionService;
    private readonly IMaintenanceService _maintenanceService;

    public StationController(
        IReservationService reservationService,
        IAvailabilityService availabilityService,
        IVehicleService vehicleService,
        IPromotionService promotionService,
        IMaintenanceService maintenanceService)
    {
        _reservationService = reservationService;
        _availabilityService = availabilityService;
        _vehicleService = vehicleService;
        _promotionService = promotionService;
        _maintenanceService = maintenanceService;
    }

    public async Task<IActionResult> Details(Guid id, DateTime? dateUtc = null)
    {
        var result = await _reservationService.GetStationDetailsAsync(id, dateUtc);
        if (!result.Success || result.Data == null)
        {
            return NotFound();
        }

        var dto = result.Data;
        var canReserve = dto.Status != EStationStatus.Maintenance;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var vehicles = new List<VehicleOptionViewModel>();

        var promotionOptions = new List<PromotionSelectOptionViewModel>();
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            var vehicleResult = await _vehicleService.GetUserVehiclesAsync(userId);
            vehicles = vehicleResult.Data?
                .Where(v => v.BatteryCapacity.HasValue && v.BatteryCapacity.Value > 0)
                .Select(v => new VehicleOptionViewModel
                {
                    Id = v.Id,
                    DisplayName = $"{v.Make} {v.Model}",
                    BatteryCapacityKwh = v.BatteryCapacity ?? 0
                })
                .ToList() ?? new List<VehicleOptionViewModel>();

            var promotionsResult = await _promotionService.GetUserPromotionsAsync(userId);
            promotionOptions = promotionsResult.Data?
                .OrderBy(p => p.Code)
                .Select(p => new PromotionSelectOptionViewModel
                {
                    Code = p.Code,
                    DisplayText = $"{p.Code} (-{p.DiscountValue:0.##}%)"
                })
                .ToList() ?? new List<PromotionSelectOptionViewModel>();
        }

        var selectedVehicle = vehicles.FirstOrDefault();
        if (selectedVehicle != null)
        {
            selectedVehicle.IsSelected = true;
        }

        var selectedConnector = dto.Connectors.FirstOrDefault(c => c.AvailableQuantity > 0) ?? dto.Connectors.FirstOrDefault();
        var batteryDelta = 80 - 25;
        var energyKwh = selectedVehicle != null ? Math.Round(selectedVehicle.BatteryCapacityKwh * batteryDelta / 100m, 2) : 0;
        var durationMinutes = selectedConnector == null
            ? 60
            : Math.Max(5, (int)Math.Ceiling((double)(energyKwh / Math.Max(1m, Math.Min(dto.MaxPower, 200m)) * 60m)));
        var costResult = await _reservationService.EstimateCostAsync(dto.Id, durationMinutes, energyKwh);

        var model = new StationDetailsViewModel
        {
            Id = dto.Id,
            CompanyId = dto.CompanyId,
            Name = dto.Name,
            Location = dto.Location,
            Status = dto.Status,
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            Connectors = dto.Connectors.Select(c => new ConnectorDetailViewModel
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
            ExistingReservations = dto.ExistingReservations
                .Select(r => new StationReservationViewModel
                {
                    StartTimeUtc = r.StartTimeUtc,
                    EndTimeUtc = r.EndTimeUtc,
                    Status = r.Status
                })
                .ToList(),
            AvailableSlots = dto.AvailableSlots.Select(s => new AvailabilitySlotViewModel
            {
                StartTimeUtc = s.StartTimeUtc,
                EndTimeUtc = s.EndTimeUtc,
                IsAvailable = s.IsAvailable
            }).ToList(),
            Vehicles = vehicles,
            SelectedVehicleId = selectedVehicle?.Id,
            SelectedConnectorId = selectedConnector?.ConnectorId,
            CalculatedEnergyKwh = energyKwh,
            CalculatedDurationMinutes = durationMinutes,
            CalculatedCost = costResult.Data?.EstimatedCost ?? 0,
            ReservationForm = new ReservationCreateViewModel
            {
                StationId = dto.Id,
                StationName = dto.Name,
                StartTimeUtc = DateTime.UtcNow.AddMinutes(30),
                EndTimeUtc = DateTime.UtcNow.AddMinutes(30 + durationMinutes),
                EstimatedEnergyKwh = energyKwh,
                EstimatedCost = costResult.Data?.EstimatedCost ?? 0,
                AvailablePromotions = promotionOptions,
                CanReserve = canReserve
            },
            IssueReportForm = new StationIssueReportViewModel
            {
                StationId = dto.Id
            }
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportIssue([Bind(Prefix = "IssueReportForm")] StationIssueReportViewModel model)
    {
        if (!User.IsInRole("Customer"))
        {
            return Forbid();
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["IssueReportError"] = App.Resources.Views.Root.Station.Details.ReportIssueValidationError;
            return RedirectToAction(nameof(Details), new { id = model.StationId });
        }

        var stationResult = await _reservationService.GetStationDetailsAsync(model.StationId);
        if (!stationResult.Success || stationResult.Data?.CompanyId == null || stationResult.Data.CompanyId == Guid.Empty)
        {
            TempData["IssueReportError"] = App.Resources.Views.Root.Station.Details.ReportIssueFailure;
            return RedirectToAction(nameof(Details), new { id = model.StationId });
        }

        var reportResult = await _maintenanceService.CreateMaintenanceAsync(
            model.StationId,
            stationResult.Data.CompanyId.Value,
            userId,
            model.IssueDescription);

        if (!reportResult.Success)
        {
            TempData["IssueReportError"] = App.Resources.Views.Root.Station.Details.ReportIssueFailure;
            return RedirectToAction(nameof(Details), new { id = model.StationId });
        }

        TempData["IssueReportSuccess"] = App.Resources.Views.Root.Station.Details.ReportIssueSuccess;
        return RedirectToAction(nameof(Details), new { id = model.StationId });
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailableSlots(Guid stationId, DateTime dateUtc, int durationMinutes = 60)
    {
        var result = await _availabilityService.GetAvailableSlotsAsync(stationId, dateUtc, durationMinutes);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(result.Errors);
        }

        return Json(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> EstimateCost(Guid stationId, int durationMinutes, decimal? estimatedKwh = null)
    {
        var result = await _reservationService.EstimateCostAsync(stationId, durationMinutes, estimatedKwh);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(result.Errors);
        }

        return Json(result.Data);
    }
}
