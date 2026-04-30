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
using WebApp.Mappers;

namespace WebApp.ApiControllers.v1.Company;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/company/{companyId:guid}/maintenance")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly AppDbContext _context;

    public MaintenanceController(IMaintenanceService maintenanceService, AppDbContext context)
    {
        _maintenanceService = maintenanceService;
        _context = context;
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
        if (!await IsCompanyMemberAsync(companyId, userId, ECompanyRole.Employee))
        {
            return Forbid();
        }

        var result = await _maintenanceService.GetIssuesAsync(companyId, includeResolved);
        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(result.Data?.Select(ApiDtoFactory.CreateDto).ToList() ?? new List<MaintenanceIssueResponse>());
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
        if (!await IsCompanyMemberAsync(companyId, userId, ECompanyRole.Employee))
        {
            return Forbid();
        }

        var result = await _maintenanceService.GetByIdAsync(id, companyId);
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
    /// Get status change history for a maintenance issue.
    /// </summary>
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(List<MaintenanceStatusHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<MaintenanceStatusHistoryResponse>>> GetIssueHistory(Guid companyId, Guid id)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId, ECompanyRole.Employee))
        {
            return Forbid();
        }

        var result = await _maintenanceService.GetStatusHistoryAsync(id, companyId);
        if (HasForbidden(result.Errors))
        {
            return Forbid();
        }

        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        var response = result.Data?.Select(ApiDtoFactory.CreateDto).ToList() ?? new List<MaintenanceStatusHistoryResponse>();

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
        if (!await IsCompanyMemberAsync(companyId, userId, ECompanyRole.Manager))
        {
            return Forbid();
        }

        if (!Enum.IsDefined(typeof(EMaintenanceStatus), request.Status))
        {
            return BadRequest(new Message("Invalid maintenance status."));
        }

        var result = await _maintenanceService.UpdateStatusAsync(id, companyId, userId, (EMaintenanceStatus)request.Status, request.Notes);
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
    /// Assign a maintenance issue to a user.
    /// </summary>
    [HttpPatch("{id:guid}/assign")]
    [ProducesResponseType(typeof(MaintenanceIssueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MaintenanceIssueResponse>> AssignIssue(Guid companyId, Guid id, [FromBody] MaintenanceAssignment request)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId, ECompanyRole.Manager))
        {
            return Forbid();
        }

        var result = await _maintenanceService.AssignAsync(id, companyId, userId, request.AssignedToUserId);
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

    private async Task<bool> IsCompanyMemberAsync(Guid companyId, Guid userId, ECompanyRole minRole)
    {
        return await _context.AppUserCompanies
            .AsNoTracking()
            .AnyAsync(uc => uc.AppUserId == userId && uc.CompanyId == companyId && uc.IsActive && uc.Role >= minRole);
    }

    private static bool HasForbidden(IEnumerable<ServiceError> errors)
    {
        return errors.Any(e => e.Code == "FORBIDDEN");
    }
}
