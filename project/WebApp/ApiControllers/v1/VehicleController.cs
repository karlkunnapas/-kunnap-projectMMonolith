using App.DTO.v1.Vehicle;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Users;
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
    private readonly IUsersModuleApi _usersModuleApi;
    private readonly IChargingModuleApi _chargingModuleApi;

    public VehicleController(IUsersModuleApi usersModuleApi, IChargingModuleApi chargingModuleApi)
    {
        _usersModuleApi = usersModuleApi;
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
        var vehicles = await _usersModuleApi.GetUserVehiclesAsync(userId);
        var connectors = await _chargingModuleApi.GetConnectorsAsync(includeInactive: false);
        var connectorNamesById = connectors.ToDictionary(x => x.Id, x => x.Name);

        var response = vehicles
            .Select(v => ApiDtoFactory.CreateDto(v, connectorNamesById))
            .ToList();
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
        var vehicle = await _usersModuleApi.GetVehicleForUserAsync(id, userId);
        if (vehicle == null)
        {
            return BadRequest(new Message("Vehicle not found."));
        }

        var connectors = await _chargingModuleApi.GetConnectorsAsync(includeInactive: false);
        var connectorNamesById = connectors.ToDictionary(x => x.Id, x => x.Name);
        return Ok(ApiDtoFactory.CreateDto(vehicle, connectorNamesById));
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
        var vehicle = await _usersModuleApi.CreateVehicleAsync(userId, new CreateUserVehicleContract
        {
            Make = request.Make,
            Model = request.Model,
            BatteryCapacity = request.BatteryCapacity,
            ConnectorIds = request.ConnectorIds
        });

        var connectors = await _chargingModuleApi.GetConnectorsAsync(includeInactive: false);
        var connectorNamesById = connectors.ToDictionary(x => x.Id, x => x.Name);
        return Ok(ApiDtoFactory.CreateDto(vehicle, connectorNamesById));
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
        var updated = await _usersModuleApi.UpdateVehicleAsync(id, userId, new UpdateUserVehicleContract
        {
            Make = request.Make,
            Model = request.Model,
            BatteryCapacity = request.BatteryCapacity,
            ConnectorIds = request.ConnectorIds
        });
        if (updated == null)
        {
            return BadRequest(new Message("Vehicle not found."));
        }

        var connectors = await _chargingModuleApi.GetConnectorsAsync(includeInactive: false);
        var connectorNamesById = connectors.ToDictionary(x => x.Id, x => x.Name);
        return Ok(ApiDtoFactory.CreateDto(updated, connectorNamesById));
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
        var deleted = await _usersModuleApi.DeleteVehicleAsync(id, userId);
        if (!deleted)
        {
            return BadRequest(new Message("Unable to delete vehicle."));
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
        var updated = await _usersModuleApi.SetConnectorCompatibilityAsync(id, userId, connectorIds);
        if (!updated)
        {
            return BadRequest(new Message("Unable to set vehicle connectors."));
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

}
