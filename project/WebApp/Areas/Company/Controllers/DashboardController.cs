using System.Security.Claims;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Areas.Company.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize(Roles = "CompanyOwner")]
public class DashboardController : Controller
{
    private readonly IOperatorDashboardService _dashboardService;
    private readonly AppDbContext _context;

    public DashboardController(IOperatorDashboardService dashboardService, AppDbContext context)
    {
        _dashboardService = dashboardService;
        _context = context;
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
        var result = await _dashboardService.GetDashboardAsync(resolvedCompany.Value, rangeFrom, rangeTo);
        if (!result.Success || result.Data == null)
        {
            return Forbid();
        }

        var model = new OperatorDashboardViewModel
        {
            CompanyId = resolvedCompany.Value,
            FromUtc = result.Data.FromUtc,
            ToUtc = result.Data.ToUtc,
            Kpis = new OperatorKpiViewModel
            {
                TotalStations = result.Data.Kpis.TotalStations,
                TotalReservations = result.Data.Kpis.TotalReservations,
                TotalSessions = result.Data.Kpis.TotalSessions,
                TotalRevenue = result.Data.Kpis.TotalRevenue,
                AvgUtilizationPercent = result.Data.Kpis.AvgUtilizationPercent,
                AvgSessionDurationMinutes = result.Data.Kpis.AvgSessionDurationMinutes,
                PeakHours = result.Data.Kpis.PeakHours
            },
            StationStatus = result.Data.StationStatus.Select(MapStationStatus).ToList(),
            MaintenanceQueue = result.Data.MaintenanceQueue.Select(MapMaintenance).ToList(),
            UtilizationTrend = result.Data.UtilizationTrend.Select(point => new ChartPointViewModel
            {
                Label = point.Label,
                Value = point.Value
            }).ToList(),
            RevenueTrend = result.Data.RevenueTrend.Select(point => new ChartPointViewModel
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
        var result = await _dashboardService.GetStationStatusAsync(resolvedCompany.Value, rangeFrom, rangeTo);
        if (!result.Success)
        {
            return Forbid();
        }

        var model = new OperatorDashboardViewModel
        {
            CompanyId = resolvedCompany.Value,
            FromUtc = rangeFrom,
            ToUtc = rangeTo,
            StationStatus = result.Data?.Select(MapStationStatus).ToList() ?? new List<StationStatusCardViewModel>()
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

        var result = await _dashboardService.GetMaintenanceQueueAsync(resolvedCompany.Value);
        if (!result.Success)
        {
            return Forbid();
        }

        var model = new OperatorDashboardViewModel
        {
            CompanyId = resolvedCompany.Value,
            MaintenanceQueue = result.Data?.Select(MapMaintenance).ToList() ?? new List<MaintenanceQueueItemViewModel>()
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
        var result = await _dashboardService.GetUtilizationTrendAsync(resolvedCompany.Value, rangeFrom, rangeTo);
        if (!result.Success)
        {
            return Forbid();
        }

        return Json(result.Data);
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
        var result = await _dashboardService.GetRevenueTrendAsync(resolvedCompany.Value, rangeFrom, rangeTo);
        if (!result.Success)
        {
            return Forbid();
        }

        return Json(result.Data);
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

        return await _context.AppUserCompanies
            .AsNoTracking()
            .Where(uc => uc.AppUserId == userId && uc.IsActive)
            .OrderByDescending(uc => uc.JoinedAtUtc)
            .Select(uc => uc.CompanyId)
            .ToListAsync();
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

    private static StationStatusCardViewModel MapStationStatus(App.BLL.DTOs.CompanyStationStatusDto station)
    {
        return new StationStatusCardViewModel
        {
            Id = station.Id,
            Name = station.Name,
            Status = station.Status,
            HealthStatus = station.HealthStatus,
            UtilizationPercent = station.UtilizationPercent,
            ActiveSessionsCount = station.ActiveSessionsCount,
            ReservationsToday = station.ReservationsToday,
            PendingMaintenanceCount = station.PendingMaintenanceCount,
            RevenueToday = station.RevenueToday
        };
    }

    private static MaintenanceQueueItemViewModel MapMaintenance(App.BLL.DTOs.MaintenanceIssueDto issue)
    {
        return new MaintenanceQueueItemViewModel
        {
            Id = issue.Id,
            StationId = issue.StationId,
            StationName = issue.StationName,
            IssueDescription = issue.IssueDescription,
            Status = issue.Status,
            ReportedAtUtc = issue.ReportedAtUtc,
            ResolvedAtUtc = issue.ResolvedAtUtc,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = issue.AssignedToUserName,
            ReporterUserName = issue.ReporterUserName,
            Notes = issue.Notes
        };
    }
}
