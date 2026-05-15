using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;
using System.Security.Claims;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer,CompanyOwner")]
public class StationController : Controller
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly IUsersModuleApi _usersModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public StationController(
        IChargingModuleApi chargingModuleApi,
        IUsersModuleApi usersModuleApi,
        ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _usersModuleApi = usersModuleApi;
        _companiesModuleApi = companiesModuleApi;
    }

    public async Task<IActionResult> Details(Guid id, DateTime? dateUtc = null)
    {
        var station = await _chargingModuleApi.GetStationByIdAsync(id);
        if (station == null)
        {
            return NotFound();
        }

        var selectedDate = (dateUtc ?? DateTime.UtcNow).Date;
        var stationReservations = await _chargingModuleApi.GetStationReservationsAsync(id);
        var connectors = await _chargingModuleApi.GetConnectorsAsync(includeInactive: false);
        var canReserve = station.Status != Shared.Contracts.Charging.EStationStatus.Maintenance;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var vehicles = new List<VehicleOptionViewModel>();

        var promotionOptions = new List<PromotionSelectOptionViewModel>();
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            var userVehicles = await _usersModuleApi.GetUserVehiclesAsync(userId);
            vehicles = userVehicles
                .Where(v => v.BatteryCapacity.HasValue && v.BatteryCapacity.Value > 0)
                .Select(v => new VehicleOptionViewModel
                {
                    Id = v.VehicleId,
                    DisplayName = $"{v.Make} {v.Model}",
                    BatteryCapacityKwh = v.BatteryCapacity ?? 0
                })
                .ToList();

            var promotions = await _companiesModuleApi.GetUserPromotionsAsync(userId);
            promotionOptions = promotions
                .Where(p => !p.IsUsed && p.Promotion != null)
                .OrderBy(p => p.Promotion!.Code)
                .Select(p => new PromotionSelectOptionViewModel
                {
                    Code = p.Promotion!.Code,
                    DisplayText = $"{p.Promotion.Code} (-{p.Promotion.DiscountValue:0.##}%)"
                })
                .ToList();
        }

        var selectedVehicle = vehicles.FirstOrDefault();
        if (selectedVehicle != null)
        {
            selectedVehicle.IsSelected = true;
        }

        var selectedConnector = station.Connectors.FirstOrDefault();
        var batteryDelta = 80 - 25;
        var energyKwh = selectedVehicle != null ? Math.Round(selectedVehicle.BatteryCapacityKwh * batteryDelta / 100m, 2) : 0;
        var durationMinutes = selectedConnector == null
            ? 60
            : Math.Max(5, (int)Math.Ceiling((double)(energyKwh / Math.Max(1m, Math.Min(station.MaxPower, 200m)) * 60m)));
        var estimatedCost = CalculateEstimatedCost(station.PricePerKwh, durationMinutes, energyKwh, station.MaxPower);

        var activeReservations = stationReservations
            .Where(r => r.Status is Shared.Contracts.Charging.EReservationStatus.Active or Shared.Contracts.Charging.EReservationStatus.Started)
            .Where(r => r.EndTimeUtc > DateTime.UtcNow)
            .OrderBy(r => r.StartTimeUtc)
            .ToList();

        var model = new StationDetailsViewModel
        {
            Id = station.Id,
            CompanyId = station.CompanyId,
            Name = station.Name,
            Location = station.Location,
            Status = station.Status,
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            Connectors = station.Connectors.Select(c => new ConnectorDetailViewModel
            {
                ConnectorId = c.Id,
                Name = connectors.FirstOrDefault(x => x.Id == c.Id)?.Name ?? "Connector",
                Quantity = 1,
                AvailableQuantity = activeReservations.Any() ? 0 : 1,
                Reservations = activeReservations.Select(r => new ReservedTimeRangeViewModel
                {
                    StartTimeUtc = r.StartTimeUtc,
                    EndTimeUtc = r.EndTimeUtc
                }).ToList()
            }).ToList(),
            ExistingReservations = activeReservations
                .Select(r => new StationReservationViewModel
                {
                    StartTimeUtc = r.StartTimeUtc,
                    EndTimeUtc = r.EndTimeUtc,
                    Status = r.Status
                })
                .ToList(),
            AvailableSlots = BuildAvailabilitySlots(activeReservations, selectedDate, durationMinutes),
            Vehicles = vehicles,
            SelectedVehicleId = selectedVehicle?.Id,
            SelectedConnectorId = selectedConnector?.Id,
            CalculatedEnergyKwh = energyKwh,
            CalculatedDurationMinutes = durationMinutes,
            CalculatedCost = estimatedCost,
            ReservationForm = new ReservationCreateViewModel
            {
                StationId = station.Id,
                StationName = station.Name,
                StartTimeUtc = DateTime.UtcNow.AddMinutes(30),
                EndTimeUtc = DateTime.UtcNow.AddMinutes(30 + durationMinutes),
                EstimatedEnergyKwh = energyKwh,
                EstimatedCost = estimatedCost,
                AvailablePromotions = promotionOptions,
                CanReserve = canReserve
            },
            IssueReportForm = new StationIssueReportViewModel
            {
                StationId = station.Id
            }
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportIssue([Bind(Prefix = "IssueReportForm")] StationIssueReportViewModel model)
    {
        var companySlug = RouteData.Values["companySlug"]?.ToString();

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
            return RedirectToAction(nameof(Details), new { id = model.StationId, companySlug });
        }

        var station = await _chargingModuleApi.GetStationByIdAsync(model.StationId);
        if (station?.CompanyId == null || station.CompanyId == Guid.Empty)
        {
            TempData["IssueReportError"] = App.Resources.Views.Root.Station.Details.ReportIssueFailure;
            return RedirectToAction(nameof(Details), new { id = model.StationId, companySlug });
        }

        var reportResult = await _chargingModuleApi.CreateMaintenanceAsync(new MaintenanceContract
        {
            Id = Guid.NewGuid(),
            CompanyId = station.CompanyId.Value,
            ChargingStationId = model.StationId,
            StationName = station.Name,
            ReportedByUserId = userId,
            IssueDescription = model.IssueDescription,
            Status = Shared.Contracts.Charging.EMaintenanceStatus.Reported,
            ReportedAtUtc = DateTime.UtcNow,
            ResolvedAtUtc = null,
            AssignedToUserId = null,
            Notes = null
        }, actorUserName: User.Identity?.Name ?? userId.ToString());

        if (reportResult == null)
        {
            TempData["IssueReportError"] = App.Resources.Views.Root.Station.Details.ReportIssueFailure;
            return RedirectToAction(nameof(Details), new { id = model.StationId, companySlug });
        }

        TempData["IssueReportSuccess"] = App.Resources.Views.Root.Station.Details.ReportIssueSuccess;
        return RedirectToAction(nameof(Details), new { id = model.StationId, companySlug });
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailableSlots(Guid stationId, DateTime dateUtc, int durationMinutes = 60)
    {
        var station = await _chargingModuleApi.GetStationByIdAsync(stationId);
        if (station == null)
        {
            return BadRequest();
        }

        var stationReservations = await _chargingModuleApi.GetStationReservationsAsync(stationId);
        var activeReservations = stationReservations
            .Where(r => r.Status is Shared.Contracts.Charging.EReservationStatus.Active or Shared.Contracts.Charging.EReservationStatus.Started)
            .ToList();
        var slots = BuildAvailabilitySlots(activeReservations, dateUtc.Date, durationMinutes);
        return Json(slots);
    }

    [HttpGet]
    public async Task<IActionResult> EstimateCost(Guid stationId, int durationMinutes, decimal? estimatedKwh = null)
    {
        var station = await _chargingModuleApi.GetStationByIdAsync(stationId);
        if (station == null)
        {
            return BadRequest();
        }

        var cost = CalculateEstimatedCost(station.PricePerKwh, durationMinutes, estimatedKwh, station.MaxPower);
        return Json(new { estimatedCost = cost, durationMinutes });
    }

    private static decimal CalculateEstimatedCost(decimal pricePerKwh, int durationMinutes, decimal? estimatedKwh, decimal stationMaxPower)
    {
        if (durationMinutes <= 0)
        {
            return 0m;
        }

        var energy = estimatedKwh
            ?? Math.Round((durationMinutes / 60m) * Math.Max(1m, Math.Min(stationMaxPower, 200m)) * 0.6m, 2, MidpointRounding.AwayFromZero);
        if (energy < 0m)
        {
            energy = 0m;
        }

        return Math.Round(pricePerKwh * energy, 2, MidpointRounding.AwayFromZero);
    }

    private static List<AvailabilitySlotViewModel> BuildAvailabilitySlots(
        IReadOnlyCollection<ReservationContract> activeReservations,
        DateTime dayUtc,
        int durationMinutes)
    {
        var slotDuration = Math.Max(5, durationMinutes);
        var start = dayUtc.Date;
        var end = start.AddDays(1);
        var slots = new List<AvailabilitySlotViewModel>();

        for (var cursor = start; cursor < end; cursor = cursor.AddMinutes(slotDuration))
        {
            var slotEnd = cursor.AddMinutes(slotDuration);
            var overlap = activeReservations.Any(r => r.StartTimeUtc < slotEnd && cursor < r.EndTimeUtc);
            slots.Add(new AvailabilitySlotViewModel
            {
                StartTimeUtc = cursor,
                EndTimeUtc = slotEnd,
                IsAvailable = !overlap
            });
        }

        return slots;
    }

}
