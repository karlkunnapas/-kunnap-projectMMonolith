using App.Domain;

namespace App.BLL.DTOs;

public class OperatorDashboardDto
{
    public OperatorKpiDto Kpis { get; set; } = new();
    public List<CompanyStationStatusDto> StationStatus { get; set; } = new();
    public List<MaintenanceIssueDto> MaintenanceQueue { get; set; } = new();
    public List<ChartPointDto> UtilizationTrend { get; set; } = new();
    public List<ChartPointDto> RevenueTrend { get; set; } = new();
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}

public class OperatorKpiDto
{
    public int TotalStations { get; set; }
    public int TotalReservations { get; set; }
    public int TotalSessions { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AvgUtilizationPercent { get; set; }
    public double AvgSessionDurationMinutes { get; set; }
    public string PeakHours { get; set; } = string.Empty;
}

public class CompanyStationStatusDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public EStationStatus Status { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public decimal UtilizationPercent { get; set; }
    public int ActiveSessionsCount { get; set; }
    public int ReservationsToday { get; set; }
    public int PendingMaintenanceCount { get; set; }
    public decimal RevenueToday { get; set; }
}

public class MaintenanceIssueDto
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string IssueDescription { get; set; } = string.Empty;
    public EMaintenanceStatus Status { get; set; }
    public DateTime ReportedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string AssignedToUserName { get; set; } = string.Empty;
    public string ReporterUserName { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class MaintenanceStatusHistoryDto
{
    public DateTime AtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Changes { get; set; } = string.Empty;
}

public class CompanyStationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EStationStatus Status { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public bool IsActive { get; set; }
    public List<string> Connectors { get; set; } = new();
    public int MaintenanceIssueCount { get; set; }
}

public class CompanyStationUpsertDto
{
    public string NameEn { get; set; } = string.Empty;
    public string NameEt { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public EStationStatus Status { get; set; } = EStationStatus.Available;
    public bool IsActive { get; set; } = true;
    public List<Guid> SelectedConnectorIds { get; set; } = new();
}

public class CompanyStationConnectorOptionDto
{
    public Guid ConnectorId { get; set; }
    public string ConnectorName { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
}

public class CompanyStationFormDto
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameEt { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public EStationStatus Status { get; set; } = EStationStatus.Available;
    public bool IsActive { get; set; } = true;
    public List<Guid> SelectedConnectorIds { get; set; } = new();
    public List<CompanyStationConnectorOptionDto> AvailableConnectors { get; set; } = new();
}

public class ChartPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
