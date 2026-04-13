namespace App.DTO.v1.Company;

public class DashboardResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public DashboardKpis Kpis { get; set; } = new();
    public List<StationStatusCard> StationStatus { get; set; } = new();
    public List<MaintenanceIssueResponse> MaintenanceQueue { get; set; } = new();
    public List<ChartPoint> UtilizationTrend { get; set; } = new();
    public List<ChartPoint> RevenueTrend { get; set; } = new();
}

public class DashboardKpis
{
    public int TotalStations { get; set; }
    public int TotalReservations { get; set; }
    public int TotalSessions { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AvgUtilizationPercent { get; set; }
    public double AvgSessionDurationMinutes { get; set; }
    public string PeakHours { get; set; } = default!;
}

public class StationStatusCard
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string HealthStatus { get; set; } = default!;
    public decimal UtilizationPercent { get; set; }
    public int ActiveSessionsCount { get; set; }
    public int ReservationsToday { get; set; }
    public int PendingMaintenanceCount { get; set; }
    public decimal RevenueToday { get; set; }
}

public class ChartPoint
{
    public string Label { get; set; } = default!;
    public decimal Value { get; set; }
}
