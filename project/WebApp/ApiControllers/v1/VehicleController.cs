using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.DTO.v1.Vehicle;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Helpers;

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
    private readonly IUnitOfWork _unitOfWork;

    public VehicleController(IVehicleService vehicleService, IUnitOfWork unitOfWork)
    {
        _vehicleService = vehicleService;
        _unitOfWork = unitOfWork;
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
        var response = result.Data?.Select(MapVehicle).ToList() ?? new List<VehicleResponse>();
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

        return Ok(MapVehicle(result.Data));
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
        var result = await _vehicleService.CreateVehicleAsync(userId, new VehicleCreateDto
        {
            Make = request.Make,
            Model = request.Model,
            BatteryCapacity = request.BatteryCapacity,
            ConnectorIds = request.ConnectorIds
        });

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapVehicle(result.Data));
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
        var result = await _vehicleService.UpdateVehicleAsync(id, userId, new VehicleUpdateDto
        {
            Make = request.Make,
            Model = request.Model,
            BatteryCapacity = request.BatteryCapacity,
            ConnectorIds = request.ConnectorIds
        });

        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapVehicle(result.Data));
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
        var connectors = await _unitOfWork.Connectors
            .GetQueryable()
            .Where(c => c.IsActive)
            .ToListAsync();

        var response = connectors
            .Select(c => new VehicleConnectorResponse
            {
                ConnectorId = c.Id,
                Name = c.Name.Translate() ?? c.Name.ToString() ?? string.Empty
            })
            .OrderBy(c => c.Name)
            .ToList();

        return Ok(response);
    }

    private static VehicleResponse MapVehicle(VehicleDto dto)
    {
        return new VehicleResponse
        {
            Id = dto.Id,
            Make = dto.Make,
            Model = dto.Model,
            BatteryCapacity = dto.BatteryCapacity,
            CompatibleConnectors = dto.CompatibleConnectors.Select(c => new VehicleConnectorResponse
            {
                ConnectorId = c.ConnectorId,
                Name = c.Name
            }).ToList()
        };
    }

    private static bool HasForbidden(IEnumerable<ServiceError> errors)
    {
        return errors.Any(e => e.Code == "FORBIDDEN");
    }
}
