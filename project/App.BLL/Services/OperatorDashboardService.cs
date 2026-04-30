using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;

namespace App.BLL.Services;

public class OperatorDashboardService : IOperatorDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public OperatorDashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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

        var stations = await _unitOfWork.ChargingStations.GetByCompanyAsync(companyId);
        var reservationsCount = await _unitOfWork.Reservations.GetCountByCompanyAsync(companyId, fromUtc, toUtc);
        var sessionsCount = await _unitOfWork.ChargingSessions.GetCountByCompanyAsync(companyId, fromUtc, toUtc);
        var revenue = await _unitOfWork.ChargingSessions.GetRevenueByCompanyAsync(companyId, fromUtc, toUtc);
        var avgDuration = await _unitOfWork.ChargingSessions.GetAverageDurationMinutesByCompanyAsync(companyId, fromUtc, toUtc);
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
            await GetPeakHoursAsync(companyId, fromUtc, toUtc));

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

        var stations = await _unitOfWork.ChargingStations.GetByCompanyAsync(companyId);
        var todayStartUtc = DateTime.UtcNow.Date;
        var todayEndUtc = todayStartUtc.AddDays(1);

        var stationCards = stations.Select(station =>
        {
            var connectorsCount = Math.Max(1, station.ChargingStationConnectors?.Count ?? 0);
            var activeSessions = station.ChargingSessions?.Count(session => session.EndTime == null) ?? 0;
            var reservationsToday = station.Reservations?.Count(reservation =>
                reservation.StartTime >= todayStartUtc && reservation.StartTime < todayEndUtc) ?? 0;
            var unresolvedMaintenance = station.MaintenanceIssues?.Count(issue => issue.Status != EMaintenanceStatus.Resolved) ?? 0;
            var inProgressMaintenance = station.MaintenanceIssues?.Count(issue => issue.Status == EMaintenanceStatus.InProgress) ?? 0;
            var utilization = Math.Round(Math.Min(100m, (decimal)activeSessions / connectorsCount * 100m), 2, MidpointRounding.AwayFromZero);
            var revenueToday = station.ChargingSessions?
                .Where(session => session.EndTime != null && session.StartTime >= todayStartUtc && session.StartTime < todayEndUtc)
                .Sum(session => session.Cost) ?? 0m;

            var health = inProgressMaintenance > 0
                ? "Critical"
                : unresolvedMaintenance > 0
                    ? "Warning"
                    : "Good";

            return BllDtoFactory.CreateCompanyStationStatusDto(
                station,
                health,
                utilization,
                activeSessions,
                reservationsToday,
                unresolvedMaintenance,
                revenueToday);
        }).OrderBy(card => card.Name).ToList();

        return ServiceResult<List<CompanyStationStatusDto>>.Ok(stationCards);
    }

    public async Task<ServiceResult<List<MaintenanceIssueDto>>> GetMaintenanceQueueAsync(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<MaintenanceIssueDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var issues = await _unitOfWork.Maintenances.GetOpenIssuesByCompanyAsync(companyId);
        var mapped = issues.Select(BllDtoFactory.CreateMaintenanceIssueDto).ToList();

        return ServiceResult<List<MaintenanceIssueDto>>.Ok(mapped);
    }

    public async Task<ServiceResult<List<ChartPointDto>>> GetUtilizationTrendAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<ChartPointDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var stations = await _unitOfWork.ChargingStations.GetByCompanyAsync(companyId);
        var sessions = await _unitOfWork.ChargingSessions.GetByCompanyAndRangeAsync(companyId, fromUtc, toUtc);
        var totalStations = Math.Max(1, stations.Count);

        var points = BuildDateAxis(fromUtc, toUtc)
            .Select(day =>
            {
                var countForDay = sessions.Count(session => session.StartTime.Date == day.Date);
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

        var sessions = await _unitOfWork.ChargingSessions.GetByCompanyAndRangeAsync(companyId, fromUtc, toUtc);

        var points = BuildDateAxis(fromUtc, toUtc)
            .Select(day =>
            {
                var sum = sessions
                    .Where(session => session.EndTime != null && session.StartTime.Date == day.Date)
                    .Sum(session => session.Cost);

                return BllDtoFactory.CreateChartPointDto(day.ToString("yyyy-MM-dd"), Math.Round(sum, 2, MidpointRounding.AwayFromZero));
            })
            .ToList();

        return ServiceResult<List<ChartPointDto>>.Ok(points);
    }

    private async Task<string> GetPeakHoursAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        var sessions = await _unitOfWork.ChargingSessions.GetByCompanyAndRangeAsync(companyId, fromUtc, toUtc);
        var peakHour = sessions
            .GroupBy(session => session.StartTime.Hour)
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
