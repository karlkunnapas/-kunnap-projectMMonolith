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
[Route("api/v{version:apiVersion}/company/{companyId:guid}/dashboard")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces("application/json")]
[Consumes("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public DashboardController(IChargingModuleApi chargingModuleApi, ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var from = NormalizeUtc(fromUtc, DateTime.UtcNow.Date.AddDays(-30));
        var to = NormalizeUtc(toUtc, DateTime.UtcNow);
        var response = await BuildDashboardResponseAsync(companyId, from, to);
        return Ok(response);
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var from = NormalizeUtc(fromUtc, DateTime.UtcNow.Date.AddDays(-30));
        var to = NormalizeUtc(toUtc, DateTime.UtcNow);
        return Ok(await BuildStationStatusAsync(companyId, from, to));
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        return Ok(await BuildMaintenanceQueueAsync(companyId));
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var from = NormalizeUtc(fromUtc, DateTime.UtcNow.Date.AddDays(-30));
        var to = NormalizeUtc(toUtc, DateTime.UtcNow);
        return Ok(await BuildUtilizationTrendAsync(companyId, from, to));
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
        if (!await _companiesModuleApi.HasCompanyRoleAsync(companyId, userId, "Manager"))
        {
            return Forbid();
        }

        var from = NormalizeUtc(fromUtc, DateTime.UtcNow.Date.AddDays(-30));
        var to = NormalizeUtc(toUtc, DateTime.UtcNow);
        return Ok(await BuildRevenueTrendAsync(companyId, from, to));
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

    private async Task<DashboardResponse> BuildDashboardResponseAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        var stations = await _chargingModuleApi.GetCompanyStationsAsync(companyId);
        var sessions = await _chargingModuleApi.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc);
        var reservations = await _chargingModuleApi.GetCompanyReservationsAsync(companyId, fromUtc, toUtc);
        var stationStatus = await BuildStationStatusAsync(companyId, fromUtc, toUtc);
        var maintenanceQueue = await BuildMaintenanceQueueAsync(companyId);
        var utilization = await BuildUtilizationTrendAsync(companyId, fromUtc, toUtc);
        var revenue = await BuildRevenueTrendAsync(companyId, fromUtc, toUtc);

        var avgDuration = sessions
            .Where(s => s.EndTimeUtc.HasValue)
            .Select(s => (s.EndTimeUtc!.Value - s.StartTimeUtc).TotalMinutes)
            .DefaultIfEmpty(0d)
            .Average();
        var avgUtilization = stationStatus.Count > 0
            ? Math.Round(stationStatus.Average(s => s.UtilizationPercent), 2, MidpointRounding.AwayFromZero)
            : 0m;
        var peakHours = GetPeakHours(sessions);

        return new DashboardResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Kpis = new DashboardKpis
            {
                TotalStations = stations.Count,
                TotalReservations = reservations.Count,
                TotalSessions = sessions.Count,
                TotalRevenue = sessions.Where(s => s.EndTimeUtc.HasValue).Sum(s => s.Cost),
                AvgUtilizationPercent = avgUtilization,
                AvgSessionDurationMinutes = Math.Round(avgDuration, 2, MidpointRounding.AwayFromZero),
                PeakHours = peakHours
            },
            StationStatus = stationStatus,
            MaintenanceQueue = maintenanceQueue,
            UtilizationTrend = utilization,
            RevenueTrend = revenue
        };
    }

    private async Task<List<StationStatusCard>> BuildStationStatusAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        var stations = await _chargingModuleApi.GetCompanyStationsAsync(companyId);
        var todayStartUtc = DateTime.UtcNow.Date;
        var todayEndUtc = todayStartUtc.AddDays(1);
        var sessions = await _chargingModuleApi.GetCompanyChargingSessionsAsync(companyId, todayStartUtc, todayEndUtc);
        var reservations = await _chargingModuleApi.GetCompanyReservationsAsync(companyId, todayStartUtc, todayEndUtc);
        var maintenance = await _chargingModuleApi.GetMaintenancesByCompanyAsync(companyId, includeResolved: true);

        return stations.Select(station =>
        {
            var connectorsCount = Math.Max(1, station.Connectors.Count);
            var activeSessions = sessions.Count(session => session.ChargingStationId == station.Id && session.EndTimeUtc == null);
            var reservationsToday = reservations.Count(reservation =>
                reservation.ChargingStationId == station.Id && reservation.StartTimeUtc >= todayStartUtc && reservation.StartTimeUtc < todayEndUtc);
            var unresolvedMaintenance = maintenance.Count(issue => issue.ChargingStationId == station.Id && issue.Status != EMaintenanceStatus.Resolved);
            var inProgressMaintenance = maintenance.Count(issue => issue.ChargingStationId == station.Id && issue.Status == EMaintenanceStatus.InProgress);
            var utilization = Math.Round(Math.Min(100m, (decimal)activeSessions / connectorsCount * 100m), 2, MidpointRounding.AwayFromZero);
            var revenueToday = sessions
                .Where(session => session.ChargingStationId == station.Id && session.EndTimeUtc != null && session.StartTimeUtc >= todayStartUtc && session.StartTimeUtc < todayEndUtc)
                .Sum(session => session.Cost);

            var health = inProgressMaintenance > 0
                ? "Critical"
                : unresolvedMaintenance > 0
                    ? "Warning"
                    : "Good";

            return new StationStatusCard
            {
                Id = station.Id,
                Name = station.Name,
                Status = station.Status.ToString(),
                HealthStatus = health,
                UtilizationPercent = utilization,
                ActiveSessionsCount = activeSessions,
                ReservationsToday = reservationsToday,
                PendingMaintenanceCount = unresolvedMaintenance,
                RevenueToday = revenueToday
            };
        }).OrderBy(s => s.Name).ToList();
    }

    private async Task<List<MaintenanceIssueResponse>> BuildMaintenanceQueueAsync(Guid companyId)
    {
        var issues = await _chargingModuleApi.GetMaintenancesByCompanyAsync(companyId, includeResolved: false);
        return issues.Select(issue => new MaintenanceIssueResponse
        {
            Id = issue.Id,
            StationId = issue.ChargingStationId,
            StationName = issue.StationName,
            IssueDescription = issue.IssueDescription,
            Status = issue.Status.ToString(),
            ReportedAtUtc = issue.ReportedAtUtc,
            ResolvedAtUtc = issue.ResolvedAtUtc,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = string.Empty,
            ReporterUserName = string.Empty,
            Notes = issue.Notes
        }).ToList();
    }

    private async Task<List<ChartPoint>> BuildUtilizationTrendAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        var stations = await _chargingModuleApi.GetCompanyStationsAsync(companyId);
        var sessions = await _chargingModuleApi.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc);
        var totalStations = Math.Max(1, stations.Count);

        return BuildDateAxis(fromUtc, toUtc)
            .Select(day =>
            {
                var countForDay = sessions.Count(session => session.StartTimeUtc.Date == day.Date);
                var utilization = Math.Round(Math.Min(100m, (decimal)countForDay / totalStations * 100m), 2, MidpointRounding.AwayFromZero);
                return new ChartPoint
                {
                    Label = day.ToString("yyyy-MM-dd"),
                    Value = utilization
                };
            })
            .ToList();
    }

    private async Task<List<ChartPoint>> BuildRevenueTrendAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        var sessions = await _chargingModuleApi.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc);
        return BuildDateAxis(fromUtc, toUtc)
            .Select(day => new ChartPoint
            {
                Label = day.ToString("yyyy-MM-dd"),
                Value = Math.Round(
                    sessions.Where(session => session.EndTimeUtc != null && session.StartTimeUtc.Date == day.Date).Sum(session => session.Cost),
                    2,
                    MidpointRounding.AwayFromZero)
            })
            .ToList();
    }

    private static string GetPeakHours(IReadOnlyCollection<ChargingSessionContract> sessions)
    {
        var peakHour = sessions
            .GroupBy(session => session.StartTimeUtc.Hour)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();

        var nextHour = (peakHour + 1) % 24;
        return $"{peakHour:00}:00-{nextHour:00}:00";
    }

    private static IEnumerable<DateTime> BuildDateAxis(DateTime fromUtc, DateTime toUtc)
    {
        var cursor = fromUtc.Date;
        var end = toUtc.Date;
        while (cursor <= end)
        {
            yield return cursor;
            cursor = cursor.AddDays(1);
        }
    }
}
