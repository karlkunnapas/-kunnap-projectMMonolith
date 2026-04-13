using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Company.ViewModels;
using WebAppClient.Helpers;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Areas.Company.Controllers;

public class DashboardController : CompanyBaseController
{
    public DashboardController(IApiClient apiClient) : base(apiClient)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var query = BuildRangeQuery(fromUtc, toUtc);
        var dto = await ApiClient.GetAsync<DashboardResponseDto>($"api/v1/company/{company.Value.CompanyId}/dashboard{query}");
        var localizedNameByStationId = await GetLocalizedStationNameMapAsync();
        return View(new OperatorDashboardViewModel
        {
            CompanyId = company.Value.CompanyId,
            FromUtc = dto.FromUtc,
            ToUtc = dto.ToUtc,
            Kpis = new OperatorKpiViewModel
            {
                TotalStations = dto.Kpis.TotalStations,
                TotalReservations = dto.Kpis.TotalReservations,
                TotalSessions = dto.Kpis.TotalSessions,
                TotalRevenue = dto.Kpis.TotalRevenue,
                AvgUtilizationPercent = dto.Kpis.AvgUtilizationPercent,
                AvgSessionDurationMinutes = dto.Kpis.AvgSessionDurationMinutes,
                PeakHours = dto.Kpis.PeakHours
            },
            StationStatus = dto.StationStatus.Select(s => MapStationCard(s, localizedNameByStationId)).ToList(),
            MaintenanceQueue = dto.MaintenanceQueue.Select(m => MapMaintenance(m, localizedNameByStationId)).ToList(),
            UtilizationTrend = dto.UtilizationTrend.Select(p => new ChartPointViewModel { Label = p.Label, Value = p.Value }).ToList(),
            RevenueTrend = dto.RevenueTrend.Select(p => new ChartPointViewModel { Label = p.Label, Value = p.Value }).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> StationStatus(Guid? companyId = null, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var query = BuildRangeQuery(fromUtc, toUtc);
        var items = await ApiClient.GetAsync<List<StationStatusCardDto>>($"api/v1/company/{company.Value.CompanyId}/dashboard/stations{query}");
        var localizedNameByStationId = await GetLocalizedStationNameMapAsync();
        return View(new OperatorDashboardViewModel
        {
            CompanyId = company.Value.CompanyId,
            FromUtc = fromUtc ?? DateTime.UtcNow.Date.AddDays(-30),
            ToUtc = toUtc ?? DateTime.UtcNow,
            StationStatus = items.Select(s => MapStationCard(s, localizedNameByStationId)).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> MaintenanceQueue(Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var items = await ApiClient.GetAsync<List<MaintenanceIssueResponseDto>>($"api/v1/company/{company.Value.CompanyId}/dashboard/maintenance");
        var localizedNameByStationId = await GetLocalizedStationNameMapAsync();
        return View(new OperatorDashboardViewModel
        {
            CompanyId = company.Value.CompanyId,
            MaintenanceQueue = items.Select(m => MapMaintenance(m, localizedNameByStationId)).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> UtilizationTrend(Guid? companyId = null, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var query = BuildRangeQuery(fromUtc, toUtc);
        var items = await ApiClient.GetAsync<List<ChartPointDto>>($"api/v1/company/{company.Value.CompanyId}/dashboard/utilization{query}");
        return Json(items);
    }

    [HttpGet]
    public async Task<IActionResult> RevenueTrend(Guid? companyId = null, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var query = BuildRangeQuery(fromUtc, toUtc);
        var items = await ApiClient.GetAsync<List<ChartPointDto>>($"api/v1/company/{company.Value.CompanyId}/dashboard/revenue{query}");
        return Json(items);
    }

    private static string BuildRangeQuery(DateTime? fromUtc, DateTime? toUtc)
    {
        var query = new List<string>();
        if (fromUtc.HasValue)
        {
            query.Add($"fromUtc={Uri.EscapeDataString(fromUtc.Value.ToString("O"))}");
        }
        if (toUtc.HasValue)
        {
            query.Add($"toUtc={Uri.EscapeDataString(toUtc.Value.ToString("O"))}");
        }
        return query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
    }

    private static StationStatusCardViewModel MapStationCard(StationStatusCardDto dto, IReadOnlyDictionary<Guid, string> localizedNameByStationId)
    {
        return new StationStatusCardViewModel
        {
            Id = dto.Id,
            Name = localizedNameByStationId.GetValueOrDefault(dto.Id, dto.Name),
            Status = EnumParser.ParseStation(dto.Status),
            HealthStatus = dto.HealthStatus,
            UtilizationPercent = dto.UtilizationPercent,
            ActiveSessionsCount = dto.ActiveSessionsCount,
            ReservationsToday = dto.ReservationsToday,
            PendingMaintenanceCount = dto.PendingMaintenanceCount,
            RevenueToday = dto.RevenueToday
        };
    }

    private static MaintenanceQueueItemViewModel MapMaintenance(MaintenanceIssueResponseDto dto, IReadOnlyDictionary<Guid, string> localizedNameByStationId)
    {
        return new MaintenanceQueueItemViewModel
        {
            Id = dto.Id,
            StationId = dto.StationId,
            StationName = localizedNameByStationId.GetValueOrDefault(dto.StationId, dto.StationName),
            IssueDescription = dto.IssueDescription,
            Status = EnumParser.ParseMaintenance(dto.Status),
            ReportedAtUtc = dto.ReportedAtUtc,
            ResolvedAtUtc = dto.ResolvedAtUtc,
            AssignedToUserId = dto.AssignedToUserId,
            AssignedToUserName = dto.AssignedToUserName,
            ReporterUserName = dto.ReporterUserName,
            Notes = dto.Notes
        };
    }
}
