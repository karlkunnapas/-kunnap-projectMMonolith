using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using WebApp.Areas.Company.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize]
public class DashboardController : Controller
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public DashboardController(IChargingModuleApi chargingModuleApi, ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var rangeFrom = NormalizeToUtc(fromUtc) ?? DateTime.UtcNow.Date.AddDays(-30);
        var rangeTo = NormalizeToUtc(toUtc) ?? DateTime.UtcNow;
        var dashboard = await BuildDashboardViewModelAsync(resolvedCompany.Value, rangeFrom, rangeTo);

        var model = new OperatorDashboardViewModel
        {
            CompanyId = resolvedCompany.Value,
            FromUtc = dashboard.FromUtc,
            ToUtc = dashboard.ToUtc,
            Kpis = new OperatorKpiViewModel
            {
                TotalStations = dashboard.Kpis.TotalStations,
                TotalReservations = dashboard.Kpis.TotalReservations,
                TotalSessions = dashboard.Kpis.TotalSessions,
                TotalRevenue = dashboard.Kpis.TotalRevenue,
                AvgUtilizationPercent = dashboard.Kpis.AvgUtilizationPercent,
                AvgSessionDurationMinutes = dashboard.Kpis.AvgSessionDurationMinutes,
                PeakHours = dashboard.Kpis.PeakHours
            },
            StationStatus = dashboard.StationStatus,
            MaintenanceQueue = dashboard.MaintenanceQueue,
            UtilizationTrend = dashboard.UtilizationTrend.Select(point => new ChartPointViewModel
            {
                Label = point.Label,
                Value = point.Value
            }).ToList(),
            RevenueTrend = dashboard.RevenueTrend.Select(point => new ChartPointViewModel
            {
                Label = point.Label,
                Value = point.Value
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> StationStatus(Guid? companyId = null, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var rangeFrom = NormalizeToUtc(fromUtc) ?? DateTime.UtcNow.Date.AddDays(-30);
        var rangeTo = NormalizeToUtc(toUtc) ?? DateTime.UtcNow;

        var model = new OperatorDashboardViewModel
        {
            CompanyId = resolvedCompany.Value,
            FromUtc = rangeFrom,
            ToUtc = rangeTo,
            StationStatus = await BuildStationStatusAsync(resolvedCompany.Value)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> MaintenanceQueue(Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var model = new OperatorDashboardViewModel
        {
            CompanyId = resolvedCompany.Value,
            MaintenanceQueue = await BuildMaintenanceQueueAsync(resolvedCompany.Value)
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> UtilizationTrend(Guid? companyId = null, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var rangeFrom = NormalizeToUtc(fromUtc) ?? DateTime.UtcNow.Date.AddDays(-30);
        var rangeTo = NormalizeToUtc(toUtc) ?? DateTime.UtcNow;
        return Json(await BuildUtilizationTrendAsync(resolvedCompany.Value, rangeFrom, rangeTo));
    }

    [HttpGet]
    public async Task<IActionResult> RevenueTrend(Guid? companyId = null, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var rangeFrom = NormalizeToUtc(fromUtc) ?? DateTime.UtcNow.Date.AddDays(-30);
        var rangeTo = NormalizeToUtc(toUtc) ?? DateTime.UtcNow;
        return Json(await BuildRevenueTrendAsync(resolvedCompany.Value, rangeFrom, rangeTo));
    }

    private async Task<Guid?> ResolveCompanyAsync(Guid? requestedCompanyId)
    {
        var membershipCompanyIds = await ResolveMembershipCompanyIdsAsync();
        if (membershipCompanyIds.Count == 0)
        {
            return null;
        }

        var resolvedCompanyId = requestedCompanyId ?? membershipCompanyIds[0];
        return membershipCompanyIds.Contains(resolvedCompanyId) ? resolvedCompanyId : null;
    }

    private async Task<List<Guid>> ResolveMembershipCompanyIdsAsync()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return new List<Guid>();
        }

        var memberships = await _companiesModuleApi.GetUserCompaniesAsync(userId);
        return memberships
            .Where(m => IsAtLeastManagerRole(m.Role))
            .Select(m => m.CompanyId)
            .ToList();
    }

    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Local).ToUniversalTime()
        };
    }

    private async Task<OperatorDashboardViewModel> BuildDashboardViewModelAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        var stations = await _chargingModuleApi.GetCompanyStationsAsync(companyId);
        var sessions = await _chargingModuleApi.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc);
        var reservations = await _chargingModuleApi.GetCompanyReservationsAsync(companyId, fromUtc, toUtc);
        var stationStatus = await BuildStationStatusAsync(companyId);
        var maintenanceQueue = await BuildMaintenanceQueueAsync(companyId);
        var utilizationTrend = await BuildUtilizationTrendAsync(companyId, fromUtc, toUtc);
        var revenueTrend = await BuildRevenueTrendAsync(companyId, fromUtc, toUtc);

        var avgDuration = sessions
            .Where(s => s.EndTimeUtc.HasValue)
            .Select(s => (s.EndTimeUtc!.Value - s.StartTimeUtc).TotalMinutes)
            .DefaultIfEmpty(0d)
            .Average();
        var avgUtilization = stationStatus.Count > 0
            ? Math.Round(stationStatus.Average(s => s.UtilizationPercent), 2, MidpointRounding.AwayFromZero)
            : 0m;

        return new OperatorDashboardViewModel
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Kpis = new OperatorKpiViewModel
            {
                TotalStations = stations.Count,
                TotalReservations = reservations.Count,
                TotalSessions = sessions.Count,
                TotalRevenue = sessions.Where(s => s.EndTimeUtc.HasValue).Sum(s => s.Cost),
                AvgUtilizationPercent = avgUtilization,
                AvgSessionDurationMinutes = Math.Round(avgDuration, 2, MidpointRounding.AwayFromZero),
                PeakHours = GetPeakHours(sessions)
            },
            StationStatus = stationStatus,
            MaintenanceQueue = maintenanceQueue,
            UtilizationTrend = utilizationTrend,
            RevenueTrend = revenueTrend
        };
    }

    private async Task<List<StationStatusCardViewModel>> BuildStationStatusAsync(Guid companyId)
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
            var unresolvedMaintenance = maintenance.Count(issue => issue.ChargingStationId == station.Id && issue.Status != Shared.Contracts.Charging.EMaintenanceStatus.Resolved);
            var inProgressMaintenance = maintenance.Count(issue => issue.ChargingStationId == station.Id && issue.Status == Shared.Contracts.Charging.EMaintenanceStatus.InProgress);
            var utilization = Math.Round(Math.Min(100m, (decimal)activeSessions / connectorsCount * 100m), 2, MidpointRounding.AwayFromZero);
            var revenueToday = sessions
                .Where(session => session.ChargingStationId == station.Id && session.EndTimeUtc != null && session.StartTimeUtc >= todayStartUtc && session.StartTimeUtc < todayEndUtc)
                .Sum(session => session.Cost);

            var health = inProgressMaintenance > 0
                ? "Critical"
                : unresolvedMaintenance > 0
                    ? "Warning"
                    : "Good";

            return new StationStatusCardViewModel
            {
                Id = station.Id,
                Name = station.Name,
                Status = station.Status,
                HealthStatus = health,
                UtilizationPercent = utilization,
                ActiveSessionsCount = activeSessions,
                ReservationsToday = reservationsToday,
                PendingMaintenanceCount = unresolvedMaintenance,
                RevenueToday = revenueToday
            };
        }).OrderBy(s => s.Name).ToList();
    }

    private async Task<List<MaintenanceQueueItemViewModel>> BuildMaintenanceQueueAsync(Guid companyId)
    {
        var issues = await _chargingModuleApi.GetMaintenancesByCompanyAsync(companyId, includeResolved: false);
        return issues.Select(issue => new MaintenanceQueueItemViewModel
        {
            Id = issue.Id,
            StationId = issue.ChargingStationId,
            StationName = issue.StationName,
            IssueDescription = issue.IssueDescription,
            Status = issue.Status,
            ReportedAtUtc = issue.ReportedAtUtc,
            ResolvedAtUtc = issue.ResolvedAtUtc,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = string.Empty,
            ReporterUserName = string.Empty,
            Notes = issue.Notes
        }).ToList();
    }

    private async Task<List<ChartPointViewModel>> BuildUtilizationTrendAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        var stations = await _chargingModuleApi.GetCompanyStationsAsync(companyId);
        var sessions = await _chargingModuleApi.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc);
        var totalStations = Math.Max(1, stations.Count);

        return BuildDateAxis(fromUtc, toUtc)
            .Select(day =>
            {
                var countForDay = sessions.Count(session => session.StartTimeUtc.Date == day.Date);
                var utilization = Math.Round(Math.Min(100m, (decimal)countForDay / totalStations * 100m), 2, MidpointRounding.AwayFromZero);
                return new ChartPointViewModel
                {
                    Label = day.ToString("yyyy-MM-dd"),
                    Value = utilization
                };
            })
            .ToList();
    }

    private async Task<List<ChartPointViewModel>> BuildRevenueTrendAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        var sessions = await _chargingModuleApi.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc);
        return BuildDateAxis(fromUtc, toUtc)
            .Select(day => new ChartPointViewModel
            {
                Label = day.ToString("yyyy-MM-dd"),
                Value = Math.Round(
                    sessions.Where(session => session.EndTimeUtc != null && session.StartTimeUtc.Date == day.Date).Sum(session => session.Cost),
                    2,
                    MidpointRounding.AwayFromZero)
            })
            .ToList();
    }

    private static bool IsAtLeastManagerRole(string? role)
    {
        return string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);
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
