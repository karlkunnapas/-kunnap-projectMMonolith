using App.DTO.v1.Reservation;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using WebApp.Helpers;
using WebApp.Mappers;

namespace WebApp.ApiControllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reservation")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class ReservationController : ControllerBase
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public ReservationController(IChargingModuleApi chargingModuleApi, ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
    }

    /// <summary>
    /// Get all reservations for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ReservationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReservationResponse>>> GetReservations()
    {
        var userId = User.UserId();
        var result = await _chargingModuleApi.GetUserReservationsAsync(userId);
        var nowUtc = DateTime.UtcNow;
        var response = result
            .Where(r => r.EndTimeUtc > nowUtc)
            .OrderBy(r => r.StartTimeUtc)
            .Select(ApiDtoFactory.CreateDto)
            .ToList();
        return Ok(response);
    }

    /// <summary>
    /// Get a specific reservation by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationResponse>> GetReservation(Guid id)
    {
        var userId = User.UserId();
        var result = await _chargingModuleApi.GetReservationByIdForUserAsync(id, userId);
        if (result != null)
        {
            return Ok(ApiDtoFactory.CreateDto(result));
        }

        return NotFound(new Message("Reservation not found."));
    }

    /// <summary>
    /// Create a new reservation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReservationResponse>> CreateReservation([FromBody] ReservationCreate request)
    {
        var userId = User.UserId();
        var station = await _chargingModuleApi.GetStationByIdAsync(request.StationId);
        if (station == null || station.Status == EStationStatus.Maintenance)
        {
            return BadRequest(new Message("Unable to create reservation."));
        }

        var overlaps = await _chargingModuleApi.GetOverlappingReservationsAsync(
            request.StationId,
            request.StartTimeUtc,
            request.EndTimeUtc);
        if (overlaps.Any(x => x.Status is EReservationStatus.Active or EReservationStatus.Started))
        {
            return BadRequest(new Message("Unable to create reservation."));
        }

        Guid? promotionId = null;
        if (!string.IsNullOrWhiteSpace(request.PromotionCode))
        {
            var normalizedCode = request.PromotionCode.Trim();
            var userPromotion = await _companiesModuleApi.GetValidUserPromotionByCodeAsync(userId, normalizedCode);
            if (userPromotion == null || userPromotion.IsUsed || userPromotion.Promotion == null)
            {
                return BadRequest(new Message("Selected promotion is invalid or expired."));
            }

            if (userPromotion.Promotion.CompanyId.HasValue && station.CompanyId != userPromotion.Promotion.CompanyId)
            {
                return BadRequest(new Message("Selected promotion is not valid for this station company."));
            }

            promotionId = userPromotion.PromotionId;
        }

        var durationMinutes = (int)Math.Ceiling((request.EndTimeUtc - request.StartTimeUtc).TotalMinutes);
        if (durationMinutes <= 0)
        {
            return BadRequest(new Message("Unable to create reservation."));
        }

        var estimatedEnergy = request.EstimatedEnergyKwh
                              ?? Math.Round((durationMinutes / 60m) * Math.Max(1m, Math.Min(station.MaxPower, 200m)) * 0.6m, 2, MidpointRounding.AwayFromZero);
        var estimatedCost = Math.Round(Math.Max(0m, estimatedEnergy) * station.PricePerKwh, 2, MidpointRounding.AwayFromZero);

        var result = await _chargingModuleApi.CreateReservationAsync(new ReservationContract
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = request.StationId,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            ExpiresAtUtc = request.EndTimeUtc,
            CancelledAtUtc = null,
            EstimatedCost = estimatedCost,
            Status = EReservationStatus.Active,
            PromotionId = promotionId,
            StationName = station.Name
        });

        return Ok(ApiDtoFactory.CreateDto(result));
    }

    /// <summary>
    /// Activate an existing reservation to start charging.
    /// </summary>
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartReservation(Guid id)
    {
        var userId = User.UserId();
        var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(id, userId);
        if (reservation == null)
        {
            return Forbid();
        }

        var result = await _chargingModuleApi.UpdateReservationStatusAsync(
            id,
            EReservationStatus.Started,
            reservation.ExpiresAtUtc,
            reservation.CancelledAtUtc,
            EStationStatus.InUse);
        if (!result)
        {
            return BadRequest(new Message("Unable to start reservation."));
        }

        return Ok();
    }

    /// <summary>
    /// Cancel a reservation.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelReservation(Guid id)
    {
        var userId = User.UserId();
        var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(id, userId);
        if (reservation == null)
        {
            return Forbid();
        }

        var result = await _chargingModuleApi.UpdateReservationStatusAsync(
            id,
            EReservationStatus.Cancelled,
            reservation.ExpiresAtUtc,
            DateTime.UtcNow,
            EStationStatus.Available);
        if (!result)
        {
            return BadRequest(new Message("Unable to cancel reservation."));
        }

        return Ok();
    }

    /// <summary>
    /// Get promotions available to the current user.
    /// </summary>
    [HttpGet("promotions")]
    [ProducesResponseType(typeof(List<UserPromotionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserPromotionResponse>>> GetUserPromotions()
    {
        var userId = User.UserId();
        var result = await _companiesModuleApi.GetUserPromotionsAsync(userId);
        var nowUtc = DateTime.UtcNow;
        var response = result
            .Where(x =>
                !x.IsUsed
                && x.Promotion != null
                && x.Promotion.IsActive
                && x.Promotion.ValidFromUtc <= nowUtc
                && x.Promotion.ValidToUtc >= nowUtc)
            .Select(ApiDtoFactory.CreateDto)
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// Redeem a promotion code.
    /// </summary>
    [HttpPost("promotions/redeem")]
    [ProducesResponseType(typeof(UserPromotionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserPromotionResponse>> RedeemPromotion([FromBody] RedeemPromotionRequest request)
    {
        var userId = User.UserId();
        var result = await _companiesModuleApi.RedeemPromotionAsync(userId, request.Code);
        if (!result.Success || result.Promotion == null)
        {
            return BadRequest(new Message(result.ErrorMessage ?? "Unable to redeem promotion."));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Promotion, userId));
    }

    /// <summary>
    /// Remove a promotion from the current user's wallet.
    /// </summary>
    [HttpDelete("promotions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemovePromotion(Guid id)
    {
        var userId = User.UserId();
        var result = await _companiesModuleApi.RemoveUserPromotionAsync(userId, id);
        if (!result)
        {
            return BadRequest(new Message("Unable to remove promotion."));
        }

        return Ok();
    }
}
