using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.Domain;
using Shared.Contracts.Charging;
using DomainStationStatus = App.Domain.EStationStatus;

namespace App.BLL.Services;

public class OperatorDashboardService : IOperatorDashboardService
{
    private readonly IChargingModuleApi _chargingModuleApi;

    public OperatorDashboardService(IChargingModuleApi chargingModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
    }

    public async Task<ServiceResult<OperatorDashboardDto>> GetDashboardAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<OperatorDashboardDto>.Fail("VALIDATION", "Company id is required.");
        }

        if (fromUtc > toUtc)
        {
            return ServiceResult<OperatorDashboardDto>.Fail("VALIDATION", "Invalid date range.");
        }

        var stations = await _chargingModuleApi.GetCompanyStationsAsync(companyId);
        var sessions = await _chargingModuleApi.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc);
        var reservations = await _chargingModuleApi.GetCompanyReservationsAsync(companyId, fromUtc, toUtc);

        var reservationsCount = reservations.Count;
        var sessionsCount = sessions.Count;
        var revenue = sessions.Where(s => s.EndTimeUtc.HasValue).Sum(s => s.Cost);
        var avgDuration = sessions
            .Where(s => s.EndTimeUtc.HasValue)
            .Select(s => (s.EndTimeUtc!.Value - s.StartTimeUtc).TotalMinutes)
            .DefaultIfEmpty(0d)
            .Average();

        var stationStatusResult = await GetStationStatusAsync(companyId, fromUtc, toUtc);
        var maintenanceResult = await GetMaintenanceQueueAsync(companyId);
        var utilizationTrendResult = await GetUtilizationTrendAsync(companyId, fromUtc, toUtc);
        var revenueTrendResult = await GetRevenueTrendAsync(companyId, fromUtc, toUtc);

        if (!stationStatusResult.Success || !maintenanceResult.Success || !utilizationTrendResult.Success || !revenueTrendResult.Success)
        {
            return ServiceResult<OperatorDashboardDto>.Fail("FAILED", "Failed to build dashboard data.");
        }

        var kpi = BllDtoFactory.CreateOperatorKpiDto(
            stations.Count,
            reservationsCount,
            sessionsCount,
            revenue,
            stationStatusResult.Data?.Any() == true
                ? Math.Round(stationStatusResult.Data.Average(s => s.UtilizationPercent), 2, MidpointRounding.AwayFromZero)
                : 0m,
            Math.Round(avgDuration, 2, MidpointRounding.AwayFromZero),
            GetPeakHours(sessions));

        return ServiceResult<OperatorDashboardDto>.Ok(
            BllDtoFactory.CreateOperatorDashboardDto(
                kpi,
                stationStatusResult.Data ?? new List<CompanyStationStatusDto>(),
                maintenanceResult.Data ?? new List<MaintenanceIssueDto>(),
                utilizationTrendResult.Data ?? new List<ChartPointDto>(),
                revenueTrendResult.Data ?? new List<ChartPointDto>(),
                fromUtc,
                toUtc));
    }

    public async Task<ServiceResult<List<CompanyStationStatusDto>>> GetStationStatusAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<CompanyStationStatusDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var stations = await _chargingModuleApi.GetCompanyStationsAsync(companyId);
        var todayStartUtc = DateTime.UtcNow.Date;
        var todayEndUtc = todayStartUtc.AddDays(1);
        var sessions = await _chargingModuleApi.GetCompanyChargingSessionsAsync(companyId, todayStartUtc, todayEndUtc);
        var reservations = await _chargingModuleApi.GetCompanyReservationsAsync(companyId, todayStartUtc, todayEndUtc);
        var maintenance = await _chargingModuleApi.GetMaintenancesByCompanyAsync(companyId, includeResolved: true);

        var stationCards = stations.Select(station =>
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

            return new CompanyStationStatusDto
            {
                Id = station.Id,
                Name = station.Name,
                Status = MapStationStatus(station.Status),
                HealthStatus = health,
                UtilizationPercent = utilization,
                ActiveSessionsCount = activeSessions,
                ReservationsToday = reservationsToday,
                PendingMaintenanceCount = unresolvedMaintenance,
                RevenueToday = revenueToday
            };
        }).OrderBy(card => card.Name).ToList();

        return ServiceResult<List<CompanyStationStatusDto>>.Ok(stationCards);
    }

    public async Task<ServiceResult<List<MaintenanceIssueDto>>> GetMaintenanceQueueAsync(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<MaintenanceIssueDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var issues = await _chargingModuleApi.GetMaintenancesByCompanyAsync(companyId, includeResolved: false);
        var mapped = issues.Select(issue => new MaintenanceIssueDto
        {
            Id = issue.Id,
            StationId = issue.ChargingStationId,
            StationName = issue.StationName,
            IssueDescription = issue.IssueDescription,
            Status = issue.Status switch
            {
                Shared.Contracts.Charging.EMaintenanceStatus.Reported => App.Domain.EMaintenanceStatus.Reported,
                Shared.Contracts.Charging.EMaintenanceStatus.InProgress => App.Domain.EMaintenanceStatus.InProgress,
                Shared.Contracts.Charging.EMaintenanceStatus.Resolved => App.Domain.EMaintenanceStatus.Resolved,
                _ => App.Domain.EMaintenanceStatus.Reported
            },
            ReportedAtUtc = issue.ReportedAtUtc,
            ResolvedAtUtc = issue.ResolvedAtUtc,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = string.Empty,
            ReporterUserName = string.Empty,
            Notes = issue.Notes
        }).ToList();

        return ServiceResult<List<MaintenanceIssueDto>>.Ok(mapped);
    }

    public async Task<ServiceResult<List<ChartPointDto>>> GetUtilizationTrendAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<ChartPointDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var stations = await _chargingModuleApi.GetCompanyStationsAsync(companyId);
        var sessions = await _chargingModuleApi.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc);
        var totalStations = Math.Max(1, stations.Count);

        var points = BuildDateAxis(fromUtc, toUtc)
            .Select(day =>
            {
                var countForDay = sessions.Count(session => session.StartTimeUtc.Date == day.Date);
                var utilization = Math.Round(Math.Min(100m, (decimal)countForDay / totalStations * 100m), 2, MidpointRounding.AwayFromZero);
                return BllDtoFactory.CreateChartPointDto(day.ToString("yyyy-MM-dd"), utilization);
            })
            .ToList();

        return ServiceResult<List<ChartPointDto>>.Ok(points);
    }

    public async Task<ServiceResult<List<ChartPointDto>>> GetRevenueTrendAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<ChartPointDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var sessions = await _chargingModuleApi.GetCompanyChargingSessionsAsync(companyId, fromUtc, toUtc);

        var points = BuildDateAxis(fromUtc, toUtc)
            .Select(day =>
            {
                var sum = sessions
                    .Where(session => session.EndTimeUtc != null && session.StartTimeUtc.Date == day.Date)
                    .Sum(session => session.Cost);

                return BllDtoFactory.CreateChartPointDto(day.ToString("yyyy-MM-dd"), Math.Round(sum, 2, MidpointRounding.AwayFromZero));
            })
            .ToList();

        return ServiceResult<List<ChartPointDto>>.Ok(points);
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

    private static DomainStationStatus MapStationStatus(Shared.Contracts.Charging.EStationStatus status)
    {
        return status switch
        {
            Shared.Contracts.Charging.EStationStatus.Available => DomainStationStatus.Available,
            Shared.Contracts.Charging.EStationStatus.InUse => DomainStationStatus.InUse,
            Shared.Contracts.Charging.EStationStatus.Maintenance => DomainStationStatus.Maintenance,
            _ => DomainStationStatus.Available
        };
    }
}
