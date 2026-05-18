using App.DTO.v1.Company;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
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
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public StationController(IChargingModuleApi chargingModuleApi, ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var stations = await _chargingModuleApi.GetCompanyStationsAsync(companyId);
        var maintenance = await _chargingModuleApi.GetMaintenancesByCompanyAsync(companyId, includeResolved: true);
        var issueCounts = maintenance
            .GroupBy(m => m.ChargingStationId)
            .ToDictionary(g => g.Key, g => g.Count());

        return Ok(stations.Select(s => ToCompanyStationResponse(s, issueCounts)).ToList());
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(id, companyId);
        if (station == null)
        {
            return BadRequest(new Message("Station not found."));
        }

        var maintenance = await _chargingModuleApi.GetMaintenancesByCompanyAsync(companyId, includeResolved: true);
        var issueCounts = maintenance
            .GroupBy(m => m.ChargingStationId)
            .ToDictionary(g => g.Key, g => g.Count());

        return Ok(ToCompanyStationResponse(station, issueCounts));
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var connectors = await _chargingModuleApi.GetConnectorsAsync(includeInactive: false);
        if (stationId.HasValue)
        {
            var station = await _chargingModuleApi.GetCompanyStationByIdAsync(stationId.Value, companyId);
            if (station == null)
            {
                return BadRequest(new Message("Station not found."));
            }

            var assignedConnectorIds = await _chargingModuleApi.GetStationAssignedConnectorIdsAsync(station.Id);
            return Ok(new CompanyStationFormResponse
            {
                Id = station.Id,
                CompanyId = companyId,
                NameEn = station.NameTranslations.TryGetValue("en", out var en) ? en : station.Name,
                NameEt = station.NameTranslations.TryGetValue("et", out var et) ? et : station.Name,
                Location = station.Location,
                PricePerKwh = station.PricePerKwh,
                MaxPower = station.MaxPower,
                Status = station.Status.ToString(),
                IsActive = station.IsActive,
                SelectedConnectorIds = assignedConnectorIds.ToList(),
                AvailableConnectors = connectors.Select(c => new ConnectorAssignmentOption
                {
                    ConnectorId = c.Id,
                    ConnectorName = c.Name,
                    IsAssigned = assignedConnectorIds.Contains(c.Id)
                }).ToList()
            });
        }

        return Ok(new CompanyStationFormResponse
        {
            CompanyId = companyId,
            Status = EStationStatus.Available.ToString(),
            IsActive = true,
            AvailableConnectors = connectors.Select(c => new ConnectorAssignmentOption
            {
                ConnectorId = c.Id,
                ConnectorName = c.Name,
                IsAssigned = false
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(typeof(EStationStatus), request.Status))
        {
            return BadRequest(new Message("Invalid station status."));
        }

        var created = await _chargingModuleApi.CreateCompanyStationAsync(new UpsertCompanyStationContract
        {
            CompanyId = companyId,
            NameEn = request.NameEn,
            NameEt = request.NameEt,
            Location = request.Location,
            Status = (EStationStatus)request.Status,
            PricePerKwh = request.PricePerKwh,
            MaxPower = request.MaxPower,
            IsActive = request.IsActive
        });
        await _chargingModuleApi.SetStationConnectorsAsync(created.Id, request.SelectedConnectorIds);
        return Ok(ToCompanyStationResponse(created, null));
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(typeof(EStationStatus), request.Status))
        {
            return BadRequest(new Message("Invalid station status."));
        }

        var updated = await _chargingModuleApi.UpdateCompanyStationAsync(new UpsertCompanyStationContract
        {
            StationId = id,
            CompanyId = companyId,
            NameEn = request.NameEn,
            NameEt = request.NameEt,
            Location = request.Location,
            Status = (EStationStatus)request.Status,
            PricePerKwh = request.PricePerKwh,
            MaxPower = request.MaxPower,
            IsActive = request.IsActive
        });
        if (updated == null)
        {
            return BadRequest(new Message("Station not found."));
        }

        await _chargingModuleApi.SetStationConnectorsAsync(id, request.SelectedConnectorIds);
        return Ok(ToCompanyStationResponse(updated, null));
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var deleted = await _chargingModuleApi.DeleteCompanyStationAsync(id, companyId);
        if (!deleted)
        {
            return BadRequest(new Message("Unable to delete station."));
        }

        return Ok();
    }

    /// <summary>
    /// Activate or deactivate a station.
    /// </summary>
    [HttpPatch("{id:guid}/activation")]
    [ProducesResponseType(typeof(CompanyStationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanyStationResponse>> UpdateActivation(Guid companyId, Guid id, [FromBody] StationActivationUpdate request)
    {
        var userId = User.UserId();
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var updated = await _chargingModuleApi.SetCompanyStationActivationAsync(id, companyId, request.IsActive);
        if (updated == null)
        {
            return BadRequest(new Message("Station not found."));
        }

        return Ok(ToCompanyStationResponse(updated, null));
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(typeof(EStationStatus), request.Status))
        {
            return BadRequest(new Message("Invalid station status."));
        }

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(id, companyId);
        if (station == null)
        {
            return BadRequest(new Message("Station not found."));
        }

        var updated = await _chargingModuleApi.UpdateCompanyStationAsync(new UpsertCompanyStationContract
        {
            StationId = id,
            CompanyId = companyId,
            NameEn = station.NameTranslations.TryGetValue("en", out var en) ? en : station.Name,
            NameEt = station.NameTranslations.TryGetValue("et", out var et) ? et : station.Name,
            Location = station.Location,
            Status = (EStationStatus)request.Status,
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            IsActive = station.IsActive
        });
        if (updated == null)
        {
            return BadRequest(new Message("Unable to update station status."));
        }

        return Ok(ToCompanyStationResponse(updated, null));
    }

    private static CompanyStationResponse ToCompanyStationResponse(
        ChargingStationContract station,
        IReadOnlyDictionary<Guid, int>? maintenanceIssueCounts)
    {
        return new CompanyStationResponse
        {
            Id = station.Id,
            Name = station.Name,
            Location = station.Location,
            Status = station.Status.ToString(),
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            IsActive = station.IsActive,
            Connectors = station.Connectors.Select(c => c.Name).ToList(),
            MaintenanceIssueCount = maintenanceIssueCounts != null
                && maintenanceIssueCounts.TryGetValue(station.Id, out var count)
                ? count
                : 0
        };
    }
}
