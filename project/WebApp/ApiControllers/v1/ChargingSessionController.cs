using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DTO.v1.Session;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Helpers;

namespace WebApp.ApiControllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chargingsession")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class ChargingSessionController : ControllerBase
{
    private readonly IChargingSessionService _sessionService;

    public ChargingSessionController(IChargingSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <summary>
    /// Get charging session history for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SessionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionResponse>>> GetSessions()
    {
        var userId = User.UserId();
        var result = await _sessionService.GetUserSessionsAsync(userId);
        var response = result.Data?.Select(MapSession).ToList() ?? new List<SessionResponse>();
        return Ok(response);
    }

    /// <summary>
    /// Get full details for a specific session.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SessionDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionDetailResponse>> GetSession(Guid id)
    {
        var userId = User.UserId();
        var result = await _sessionService.GetSessionDetailsAsync(id, userId);
        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(new SessionDetailResponse
        {
            Id = result.Data.Id,
            StationId = result.Data.StationId,
            StationName = result.Data.StationName,
            ReservationId = result.Data.ReservationId,
            StartTimeUtc = result.Data.StartTimeUtc,
            EndTimeUtc = result.Data.EndTimeUtc,
            DurationMinutes = result.Data.DurationMinutes,
            EnergyConsumedKwh = result.Data.EnergyConsumedKwh,
            Cost = result.Data.Cost,
            BaseCostBeforeDiscount = result.Data.BaseCostBeforeDiscount,
            DiscountPercent = result.Data.DiscountPercent,
            DiscountAmount = result.Data.DiscountAmount,
            PromotionCode = result.Data.PromotionCode,
            IsActive = result.Data.IsActive
        });
    }

    /// <summary>
    /// Start a new charging session.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SessionResponse>> StartSession([FromBody] SessionStartRequest request)
    {
        var userId = User.UserId();
        var result = await _sessionService.StartSessionAsync(userId, new ChargingSessionStartRequestDto
        {
            StationId = request.StationId,
            ReservationId = request.ReservationId
        });

        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapSession(result.Data));
    }

    /// <summary>
    /// Stop an active charging session.
    /// </summary>
    [HttpPost("{id:guid}/stop")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SessionResponse>> StopSession(Guid id, [FromBody] SessionStopRequest request)
    {
        var userId = User.UserId();
        var result = await _sessionService.StopSessionAsync(userId, id, new ChargingSessionStopRequestDto
        {
            EnergyConsumedKwh = 0,
            DurationMinutes = null,
            PromotionCode = request.PromotionCode
        });

        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapSession(result.Data));
    }

    private static SessionResponse MapSession(ChargingSessionDto dto)
    {
        return new SessionResponse
        {
            Id = dto.Id,
            StationId = dto.StationId,
            StationName = dto.StationName,
            ReservationId = dto.ReservationId,
            StartTimeUtc = dto.StartTimeUtc,
            EndTimeUtc = dto.EndTimeUtc,
            EnergyConsumedKwh = dto.EnergyConsumedKwh,
            Cost = dto.Cost,
            BaseCostBeforeDiscount = dto.BaseCostBeforeDiscount,
            DiscountPercent = dto.DiscountPercent,
            DiscountAmount = dto.DiscountAmount,
            PromotionCode = dto.PromotionCode,
            IsActive = dto.IsActive
        };
    }

    private static bool HasForbidden(IEnumerable<ServiceError> errors)
    {
        return errors.Any(e => e.Code == "FORBIDDEN");
    }
}
