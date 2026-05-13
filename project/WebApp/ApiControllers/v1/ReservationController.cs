using App.DTO.v1.Reservation;
using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IReservationService _reservationService;
    private readonly IPromotionService _promotionService;

    public ReservationController(IReservationService reservationService, IPromotionService promotionService)
    {
        _reservationService = reservationService;
        _promotionService = promotionService;
    }

    /// <summary>
    /// Get all reservations for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ReservationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReservationResponse>>> GetReservations()
    {
        var userId = User.UserId();
        var result = await _reservationService.GetUserReservationsAsync(userId);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.FirstOrDefault()?.Message ?? "Unable to load reservations."));
        }

        var response = result.Data.Select(ApiDtoFactory.CreateDto).ToList();
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
        var result = await _reservationService.GetReservationDetailsAsync(id, userId);
        if (result.Success && result.Data != null)
        {
            return Ok(ApiDtoFactory.CreateDto(result.Data));
        }

        if (result.Errors.Any(e => e.Code == "FORBIDDEN"))
        {
            return Forbid();
        }

        return NotFound(new Message(result.Errors.FirstOrDefault()?.Message ?? "Reservation not found."));
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
        var dto = ApiDtoFactory.CreateDto(request);
        var result = await _reservationService.ReserveAsync(userId, dto);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.FirstOrDefault()?.Message ?? "Unable to create reservation."));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
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
        var result = await _reservationService.StartReservationAsync(id, userId);
        if (!result.Success)
        {
            var errorCode = result.Errors.FirstOrDefault()?.Code;
            if (errorCode == "FORBIDDEN")
            {
                return Forbid();
            }

            return BadRequest(new Message(result.Errors.FirstOrDefault()?.Message ?? "Unable to start reservation."));
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
        var result = await _reservationService.CancelReservationAsync(id, userId);
        if (!result.Success)
        {
            var errorCode = result.Errors.FirstOrDefault()?.Code;
            if (errorCode == "FORBIDDEN")
            {
                return Forbid();
            }

            return BadRequest(new Message(result.Errors.FirstOrDefault()?.Message ?? "Unable to cancel reservation."));
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
        var result = await _promotionService.GetUserPromotionsAsync(userId);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.FirstOrDefault()?.Message ?? "Unable to load promotions."));
        }

        var response = result.Data.Select(ApiDtoFactory.CreateDto).ToList();

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
        var result = await _promotionService.RedeemPromotionAsync(userId, request.Code);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.FirstOrDefault()?.Message ?? "Unable to redeem promotion."));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
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
        var result = await _promotionService.RemoveUserPromotionAsync(userId, id);
        if (!result.Success)
        {
            if (result.Errors.Any(e => e.Code == "FORBIDDEN"))
            {
                return Forbid();
            }

            return BadRequest(new Message(result.Errors.FirstOrDefault()?.Message ?? "Unable to remove promotion."));
        }

        return Ok();
    }
}
