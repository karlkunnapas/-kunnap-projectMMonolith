using App.DTO.v1.Session;
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
[Route("api/v{version:apiVersion}/chargingsession")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class ChargingSessionController : ControllerBase
{
    private readonly IChargingSessionService _chargingSessionService;

    public ChargingSessionController(IChargingSessionService chargingSessionService)
    {
        _chargingSessionService = chargingSessionService;
    }

    /// <summary>
    /// Get charging session history for the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SessionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionResponse>>> GetSessions()
    {
        var userId = User.UserId();
        var result = await _chargingSessionService.GetUserSessionsAsync(userId);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.FirstOrDefault()?.Message ?? "Unable to load sessions."));
        }

        var response = result.Data.Select(ApiDtoFactory.CreateDto).ToList();
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
        var result = await _chargingSessionService.GetSessionDetailsAsync(id, userId);
        if (result.Success && result.Data != null)
        {
            return Ok(ApiDtoFactory.CreateDto(result.Data));
        }

        var errorCode = result.Errors.FirstOrDefault()?.Code;
        if (errorCode == "FORBIDDEN")
        {
            return Forbid();
        }

        return BadRequest(new Message(result.Errors.FirstOrDefault()?.Message ?? "Charging session not found."));
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
        var result = await _chargingSessionService.StartSessionAsync(userId, ApiDtoFactory.CreateDto(request));
        if (!result.Success || result.Data == null)
        {
            var errorCode = result.Errors.FirstOrDefault()?.Code;
            if (errorCode == "FORBIDDEN")
            {
                return Forbid();
            }

            return BadRequest(new Message(result.Errors.FirstOrDefault()?.Message ?? "Unable to start charging session."));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
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
        var result = await _chargingSessionService.StopSessionAsync(userId, id, ApiDtoFactory.CreateDto(request));
        if (!result.Success || result.Data == null)
        {
            var errorCode = result.Errors.FirstOrDefault()?.Code;
            if (errorCode == "FORBIDDEN")
            {
                return Forbid();
            }

            return BadRequest(new Message(result.Errors.FirstOrDefault()?.Message ?? "Unable to stop charging session."));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
    }
}
