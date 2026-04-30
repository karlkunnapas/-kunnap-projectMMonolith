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
[Route("api/v{version:apiVersion}/company/{companyId:guid}/dashboard")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IOperatorDashboardService _dashboardService;
    private readonly AppDbContext _context;

    public DashboardController(IOperatorDashboardService dashboardService, AppDbContext context)
    {
        _dashboardService = dashboardService;
        _context = context;
    }

    /// <summary>
    /// Get the full operator dashboard including KPIs, trends and queue.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardResponse>> GetDashboard(Guid companyId, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var from = NormalizeUtc(fromUtc, DateTime.UtcNow.Date.AddDays(-30));
        var to = NormalizeUtc(toUtc, DateTime.UtcNow);
        var result = await _dashboardService.GetDashboardAsync(companyId, from, to);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
    }

    /// <summary>
    /// Get current status cards for all company stations.
    /// </summary>
    [HttpGet("stations")]
    [ProducesResponseType(typeof(List<StationStatusCard>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<StationStatusCard>>> GetStationStatus(Guid companyId, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var from = NormalizeUtc(fromUtc, DateTime.UtcNow.Date.AddDays(-30));
        var to = NormalizeUtc(toUtc, DateTime.UtcNow);
        var result = await _dashboardService.GetStationStatusAsync(companyId, from, to);
        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        var response = result.Data?.Select(ApiDtoFactory.CreateDto).ToList() ?? new List<StationStatusCard>();
        return Ok(response);
    }

    /// <summary>
    /// Get the current maintenance queue.
    /// </summary>
    [HttpGet("maintenance")]
    [ProducesResponseType(typeof(List<MaintenanceIssueResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<MaintenanceIssueResponse>>> GetMaintenanceQueue(Guid companyId)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var result = await _dashboardService.GetMaintenanceQueueAsync(companyId);
        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        var response = result.Data?.Select(ApiDtoFactory.CreateDto).ToList() ?? new List<MaintenanceIssueResponse>();
        return Ok(response);
    }

    /// <summary>
    /// Get utilization trend data for charting.
    /// </summary>
    [HttpGet("utilization")]
    [ProducesResponseType(typeof(List<ChartPoint>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<ChartPoint>>> GetUtilizationTrend(Guid companyId, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var from = NormalizeUtc(fromUtc, DateTime.UtcNow.Date.AddDays(-30));
        var to = NormalizeUtc(toUtc, DateTime.UtcNow);
        var result = await _dashboardService.GetUtilizationTrendAsync(companyId, from, to);
        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(result.Data?.Select(ApiDtoFactory.CreateDto).ToList() ?? new List<ChartPoint>());
    }

    /// <summary>
    /// Get revenue trend data for charting.
    /// </summary>
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(List<ChartPoint>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<ChartPoint>>> GetRevenueTrend(Guid companyId, [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null)
    {
        var userId = User.UserId();
        if (!await IsCompanyMemberAsync(companyId, userId))
        {
            return Forbid();
        }

        var from = NormalizeUtc(fromUtc, DateTime.UtcNow.Date.AddDays(-30));
        var to = NormalizeUtc(toUtc, DateTime.UtcNow);
        var result = await _dashboardService.GetRevenueTrendAsync(companyId, from, to);
        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(result.Data?.Select(ApiDtoFactory.CreateDto).ToList() ?? new List<ChartPoint>());
    }

    private async Task<bool> IsCompanyMemberAsync(Guid companyId, Guid userId, ECompanyRole minRole = ECompanyRole.Manager)
    {
        return await _context.AppUserCompanies
            .AsNoTracking()
            .AnyAsync(uc => uc.AppUserId == userId && uc.CompanyId == companyId && uc.IsActive && uc.Role >= minRole);
    }

    private static DateTime NormalizeUtc(DateTime? value, DateTime fallback)
    {
        if (!value.HasValue)
        {
            return fallback;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

}
