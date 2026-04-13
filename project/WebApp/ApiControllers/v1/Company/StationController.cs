using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.Domain;
using App.DTO.v1.Company;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Helpers;

namespace WebApp.ApiControllers.v1.Company;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/company/{companyId:guid}/station")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class StationController : ControllerBase
{
    private readonly IChargingStationCompanyService _stationService;
    private readonly AppDbContext _context;

    public StationController(IChargingStationCompanyService stationService, AppDbContext context)
    {
        _stationService = stationService;
        _context = context;
    }

    /// <summary>
    /// List all stations belonging to the company.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CompanyStationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<CompanyStationResponse>>> GetStations(Guid companyId)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var result = await _stationService.GetCompanyStationsAsync(companyId);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(result.Data.Select(MapStation).ToList());
    }

    /// <summary>
    /// Get details for a specific company station.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CompanyStationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanyStationResponse>> GetStation(Guid companyId, Guid id)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var result = await _stationService.GetStationDetailsAsync(id, companyId);
        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapStation(result.Data));
    }

    /// <summary>
    /// Get form data for creating or editing a station.
    /// </summary>
    [HttpGet("form")]
    [ProducesResponseType(typeof(CompanyStationFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanyStationFormResponse>> GetForm(Guid companyId, [FromQuery] Guid? stationId = null)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var result = stationId.HasValue
            ? await _stationService.GetEditFormAsync(stationId.Value, companyId)
            : await _stationService.GetCreateFormAsync(companyId);

        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(new CompanyStationFormResponse
        {
            Id = result.Data.Id,
            CompanyId = result.Data.CompanyId,
            NameEn = result.Data.NameEn,
            NameEt = result.Data.NameEt,
            Location = result.Data.Location,
            PricePerKwh = result.Data.PricePerKwh,
            MaxPower = result.Data.MaxPower,
            Status = result.Data.Status.ToString(),
            IsActive = result.Data.IsActive,
            SelectedConnectorIds = result.Data.SelectedConnectorIds,
            AvailableConnectors = result.Data.AvailableConnectors.Select(c => new ConnectorAssignmentOption
            {
                ConnectorId = c.ConnectorId,
                ConnectorName = c.ConnectorName,
                IsAssigned = c.IsAssigned
            }).ToList()
        });
    }

    /// <summary>
    /// Create a new charging station.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CompanyStationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CompanyStationResponse>> CreateStation(Guid companyId, [FromBody] CompanyStationUpsert request)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(typeof(EStationStatus), request.Status))
        {
            return BadRequest(new Message("Invalid station status."));
        }

        var userName = User.Identity?.Name ?? userId.ToString();
        var result = await _stationService.CreateStationAsync(companyId, userId, userName, new CompanyStationUpsertDto
        {
            NameEn = request.NameEn,
            NameEt = request.NameEt,
            Location = request.Location,
            PricePerKwh = request.PricePerKwh,
            MaxPower = request.MaxPower,
            Status = (EStationStatus)request.Status,
            IsActive = request.IsActive,
            SelectedConnectorIds = request.SelectedConnectorIds
        });

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapStation(result.Data));
    }

    /// <summary>
    /// Update a charging station.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CompanyStationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CompanyStationResponse>> UpdateStation(Guid companyId, Guid id, [FromBody] CompanyStationUpsert request)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(typeof(EStationStatus), request.Status))
        {
            return BadRequest(new Message("Invalid station status."));
        }

        var userName = User.Identity?.Name ?? userId.ToString();
        var result = await _stationService.UpdateStationAsync(id, companyId, userId, userName, new CompanyStationUpsertDto
        {
            NameEn = request.NameEn,
            NameEt = request.NameEt,
            Location = request.Location,
            PricePerKwh = request.PricePerKwh,
            MaxPower = request.MaxPower,
            Status = (EStationStatus)request.Status,
            IsActive = request.IsActive,
            SelectedConnectorIds = request.SelectedConnectorIds
        });

        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapStation(result.Data));
    }

    /// <summary>
    /// Delete a charging station.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteStation(Guid companyId, Guid id)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var userName = User.Identity?.Name ?? userId.ToString();
        var result = await _stationService.DeleteStationAsync(id, companyId, userId, userName);
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
    /// Change the operational status of a station.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(CompanyStationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanyStationResponse>> UpdateStatus(Guid companyId, Guid id, [FromBody] StationStatusUpdate request)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(typeof(EStationStatus), request.Status))
        {
            return BadRequest(new Message("Invalid station status."));
        }

        var userName = User.Identity?.Name ?? userId.ToString();
        var result = await _stationService.UpdateStatusAsync(id, companyId, userId, userName, (EStationStatus)request.Status);
        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(MapStation(result.Data));
    }

    private async Task<bool> IsCompanyMemberAsync(Guid companyId, Guid userId, ECompanyRole minRole = ECompanyRole.Manager)
    {
        return await _context.AppUserCompanies
            .AsNoTracking()
            .AnyAsync(uc => uc.AppUserId == userId && uc.CompanyId == companyId && uc.IsActive && uc.Role >= minRole);
    }

    private static CompanyStationResponse MapStation(CompanyStationDto dto)
    {
        return new CompanyStationResponse
        {
            Id = dto.Id,
            Name = dto.Name,
            Location = dto.Location,
            Status = dto.Status.ToString(),
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            IsActive = dto.IsActive,
            Connectors = dto.Connectors,
            MaintenanceIssueCount = dto.MaintenanceIssueCount
        };
    }

    private static bool HasForbidden(IEnumerable<ServiceError> errors)
    {
        return errors.Any(e => e.Code == "FORBIDDEN");
    }
}
