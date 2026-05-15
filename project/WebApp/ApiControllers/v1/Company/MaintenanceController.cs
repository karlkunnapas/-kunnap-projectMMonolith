using App.DTO.v1.Company;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;
using WebApp.Helpers;

namespace WebApp.ApiControllers.v1.Company;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/company/{companyId:guid}/maintenance")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class MaintenanceController : ControllerBase
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private readonly IUsersModuleApi _usersModuleApi;

    public MaintenanceController(
        IChargingModuleApi chargingModuleApi,
        ICompaniesModuleApi companiesModuleApi,
        IUsersModuleApi usersModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
        _usersModuleApi = usersModuleApi;
    }

    /// <summary>
    /// List all maintenance issues for the company.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<MaintenanceIssueResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<MaintenanceIssueResponse>>> GetIssues(Guid companyId, [FromQuery] bool includeResolved = true)
    {
        var userId = User.UserId();
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Employee"))
        {
            return Forbid();
        }

        var issues = await _chargingModuleApi.GetMaintenancesByCompanyAsync(companyId, includeResolved);
        var response = new List<MaintenanceIssueResponse>(issues.Count);
        foreach (var issue in issues)
        {
            response.Add(await ToMaintenanceIssueResponseAsync(issue));
        }

        return Ok(response);
    }

    /// <summary>
    /// Get a specific maintenance issue.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MaintenanceIssueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MaintenanceIssueResponse>> GetIssue(Guid companyId, Guid id)
    {
        var userId = User.UserId();
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Employee"))
        {
            return Forbid();
        }

        var issue = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, companyId);
        if (issue == null)
        {
            return BadRequest(new Message("Maintenance issue not found."));
        }

        return Ok(await ToMaintenanceIssueResponseAsync(issue));
    }

    /// <summary>
    /// Get status change history for a maintenance issue.
    /// </summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(List<MaintenanceStatusHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<MaintenanceStatusHistoryResponse>>> GetIssueHistory(Guid companyId, Guid id)
    {
        var userId = User.UserId();
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Employee"))
        {
            return Forbid();
        }

        var issue = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, companyId);
        if (issue == null)
        {
            return BadRequest(new Message("Maintenance issue not found."));
        }

        var trail = await _companiesModuleApi.GetAuditTrailAsync("Maintenance", id, companyId);
        var response = trail.Entries
            .Select(e => new MaintenanceStatusHistoryResponse
            {
                AtUtc = e.AtUtc,
                Action = e.Action,
                Actor = e.UserName,
                Changes = e.ChangesJson ?? string.Empty
            })
            .ToList();
        return Ok(response);
    }

    /// <summary>
    /// Update the status of a maintenance issue.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(MaintenanceIssueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MaintenanceIssueResponse>> UpdateIssueStatus(Guid companyId, Guid id, [FromBody] MaintenanceStatusUpdate request)
    {
        var userId = User.UserId();
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Employee"))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(typeof(EMaintenanceStatus), request.Status))
        {
            return BadRequest(new Message("Invalid maintenance status."));
        }

        var issue = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, companyId);
        if (issue == null)
        {
            return BadRequest(new Message("Maintenance issue not found."));
        }

        var targetStatus = (EMaintenanceStatus)request.Status;
        DateTime? resolvedAtUtc = targetStatus == EMaintenanceStatus.Resolved ? DateTime.UtcNow : null;
        var actorUserName = User.Identity?.Name ?? userId.ToString();
        var updated = await _chargingModuleApi.UpdateMaintenanceStatusAsync(id, targetStatus, request.Notes, resolvedAtUtc, actorUserName: actorUserName);
        if (!updated)
        {
            return BadRequest(new Message("Unable to update maintenance issue."));
        }

        var stationStatus = targetStatus == EMaintenanceStatus.Resolved
            ? EStationStatus.Available
            : EStationStatus.Maintenance;
        await _chargingModuleApi.UpdateStationStatusAsync(issue.ChargingStationId, stationStatus);

        var refreshed = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, companyId);
        if (refreshed == null)
        {
            return BadRequest(new Message("Maintenance issue not found."));
        }

        return Ok(await ToMaintenanceIssueResponseAsync(refreshed));
    }

    /// <summary>
    /// Assign a maintenance issue to a user.
    /// </summary>
    [HttpPatch("{id:guid}/assign")]
    [ProducesResponseType(typeof(MaintenanceIssueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MaintenanceIssueResponse>> AssignIssue(Guid companyId, Guid id, [FromBody] MaintenanceAssignment request)
    {
        var userId = User.UserId();
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Employee"))
        {
            return Forbid();
        }

        var issue = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, companyId);
        if (issue == null)
        {
            return BadRequest(new Message("Maintenance issue not found."));
        }

        var actorUserName = User.Identity?.Name ?? userId.ToString();
        var assigned = await _chargingModuleApi.AssignMaintenanceAsync(id, request.AssignedToUserId, actorUserName: actorUserName);
        if (!assigned)
        {
            return BadRequest(new Message("Unable to assign maintenance issue."));
        }

        var refreshed = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, companyId);
        if (refreshed == null)
        {
            return BadRequest(new Message("Maintenance issue not found."));
        }

        return Ok(await ToMaintenanceIssueResponseAsync(refreshed));
    }

    private async Task<MaintenanceIssueResponse> ToMaintenanceIssueResponseAsync(MaintenanceContract issue)
    {
        var assignedName = issue.AssignedToUserId.HasValue
            ? await _usersModuleApi.GetUserDisplayNameAsync(issue.AssignedToUserId.Value) ?? string.Empty
            : string.Empty;
        var reporterName = issue.ReportedByUserId.HasValue
            ? await _usersModuleApi.GetUserDisplayNameAsync(issue.ReportedByUserId.Value) ?? string.Empty
            : string.Empty;

        return new MaintenanceIssueResponse
        {
            Id = issue.Id,
            StationId = issue.ChargingStationId,
            StationName = issue.StationName,
            IssueDescription = issue.IssueDescription,
            Status = issue.Status.ToString(),
            ReportedAtUtc = issue.ReportedAtUtc,
            ResolvedAtUtc = issue.ResolvedAtUtc,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = assignedName,
            ReporterUserName = reporterName,
            Notes = issue.Notes
        };
    }
}
