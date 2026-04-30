using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DTO.v1.Reservation;
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
        var response = result.Data?.Select(ApiDtoFactory.CreateDto).ToList() ?? new List<ReservationResponse>();
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

        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return NotFound(new Message("Reservation not found."));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
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
        var result = await _reservationService.ReserveAsync(userId, ApiDtoFactory.CreateDto(request));

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
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
        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
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
        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
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
        var response = result.Data?.Select(ApiDtoFactory.CreateDto).ToList() ?? new List<UserPromotionResponse>();

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
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
    }

    private static bool HasForbidden(IEnumerable<ServiceError> errors)
    {
        return errors.Any(e => e.Code == "FORBIDDEN");
    }
}
