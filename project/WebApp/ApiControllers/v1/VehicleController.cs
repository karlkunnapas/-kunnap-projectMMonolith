using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DTO.v1.Vehicle;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using WebApp.Helpers;
using WebApp.Mappers;

namespace WebApp.ApiControllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vehicle")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class VehicleController : ControllerBase
{
    private readonly IVehicleService _vehicleService;
    private readonly IChargingModuleApi _chargingModuleApi;

    public VehicleController(IVehicleService vehicleService, IChargingModuleApi chargingModuleApi)
    {
        _vehicleService = vehicleService;
        _chargingModuleApi = chargingModuleApi;
    }

    /// <summary>
    /// Get all vehicles registered by the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<VehicleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VehicleResponse>>> GetVehicles()
    {
        var userId = User.UserId();
        var result = await _vehicleService.GetUserVehiclesAsync(userId);
        var response = result.Data?.Select(ApiDtoFactory.CreateDto).ToList() ?? new List<VehicleResponse>();
        return Ok(response);
    }

    /// <summary>
    /// Get a specific vehicle.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VehicleResponse>> GetVehicle(Guid id)
    {
        var userId = User.UserId();
        var result = await _vehicleService.GetVehicleForUserAsync(id, userId);
        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
    }

    /// <summary>
    /// Register a new vehicle.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VehicleResponse>> CreateVehicle([FromBody] VehicleCreate request)
    {
        var userId = User.UserId();
        var result = await _vehicleService.CreateVehicleAsync(userId, ApiDtoFactory.CreateDto(request));

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
    }

    /// <summary>
    /// Update a vehicle.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(VehicleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VehicleResponse>> UpdateVehicle(Guid id, [FromBody] VehicleUpdate request)
    {
        var userId = User.UserId();
        var result = await _vehicleService.UpdateVehicleAsync(id, userId, ApiDtoFactory.CreateDto(request));

        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
    }

    /// <summary>
    /// Delete a vehicle.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteVehicle(Guid id)
    {
        var userId = User.UserId();
        var result = await _vehicleService.DeleteVehicleAsync(id, userId);
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
    /// Set compatible connectors for a vehicle.
    /// </summary>
    [HttpPut("{id:guid}/connectors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetCompatibleConnectors(Guid id, [FromBody] List<Guid> connectorIds)
    {
        var userId = User.UserId();
        var result = await _vehicleService.SetConnectorCompatibilityAsync(id, userId, connectorIds);
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
    /// List all available connector types.
    /// </summary>
    [HttpGet("connectors")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<VehicleConnectorResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VehicleConnectorResponse>>> GetConnectors()
    {
        var connectors = await _chargingModuleApi.GetConnectorsAsync(includeInactive: false);

        var response = connectors
            .Select(ApiDtoFactory.CreateDto)
            .OrderBy(c => c.Name)
            .ToList();

        return Ok(response);
    }

    private static bool HasForbidden(IEnumerable<ServiceError> errors)
    {
        return errors.Any(e => e.Code == "FORBIDDEN");
    }
}
