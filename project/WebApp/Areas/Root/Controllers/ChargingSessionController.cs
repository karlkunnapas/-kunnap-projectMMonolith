using System.Security.Claims;
using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer")]
public class ChargingSessionController : Controller
{
    private readonly IChargingSessionService _chargingSessionService;
    private readonly IReservationService _reservationService;

    public ChargingSessionController(
        IChargingSessionService chargingSessionService,
        IReservationService reservationService)
    {
        _chargingSessionService = chargingSessionService;
        _reservationService = reservationService;
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

        var result = await _chargingSessionService.StartSessionAsync(userId.Value, new ChargingSessionStartRequestDto
        {
            StationId = model.StationId,
            ReservationId = model.ReservationId
        });

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

        return View(MapToDetail(result.Data));
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

        var result = await _chargingSessionService.StopSessionAsync(userId.Value, model.Id, new ChargingSessionStopRequestDto());

        if (!result.Success)
        {
            if (result.Errors.Any(e => e.Code == "FORBIDDEN"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
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
            IsActive = session.IsActive
        };
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
