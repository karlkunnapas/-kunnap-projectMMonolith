using System.ComponentModel.DataAnnotations;
using Shared.Contracts.Charging;

namespace WebApp.Areas.Company.ViewModels;

public class OperatorDashboardViewModel
{
    public Guid CompanyId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public OperatorKpiViewModel Kpis { get; set; } = new();
    public List<StationStatusCardViewModel> StationStatus { get; set; } = new();
    public List<MaintenanceQueueItemViewModel> MaintenanceQueue { get; set; } = new();
    public List<ChartPointViewModel> UtilizationTrend { get; set; } = new();
    public List<ChartPointViewModel> RevenueTrend { get; set; } = new();
}

public class OperatorKpiViewModel
{
    public int TotalStations { get; set; }
    public int TotalReservations { get; set; }
    public int TotalSessions { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AvgUtilizationPercent { get; set; }
    public double AvgSessionDurationMinutes { get; set; }
    public string PeakHours { get; set; } = string.Empty;
}

public class StationStatusCardViewModel
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

public class MaintenanceQueueItemViewModel
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

public class ChartPointViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class CompanyStationListViewModel
{
    public Guid CompanyId { get; set; }
    public List<CompanyStationItemViewModel> Stations { get; set; } = new();
}

public class CompanyStationItemViewModel
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

public class CompanyStationDetailsViewModel : CompanyStationItemViewModel
{
    public List<MaintenanceQueueItemViewModel> RecentMaintenance { get; set; } = new();
}

public class CompanyStationFormViewModel
{
    public Guid? Id { get; set; }
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string NameEn { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string NameEt { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Location { get; set; } = string.Empty;

    public decimal PricePerKwh { get; set; }

    public decimal MaxPower { get; set; }

    [Required]
    public EStationStatus Status { get; set; } = EStationStatus.Available;

    public bool IsActive { get; set; } = true;
    public List<Guid> SelectedConnectorIds { get; set; } = new();
    public List<StationConnectorViewModel> AvailableConnectors { get; set; } = new();
}

public class StationConnectorViewModel
{
    public Guid ConnectorId { get; set; }
    public string ConnectorName { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
}

public class MaintenanceListViewModel
{
    public Guid CompanyId { get; set; }
    public bool IncludeResolved { get; set; }
    public bool CanAccessDashboard { get; set; }
    public List<MaintenanceQueueItemViewModel> Issues { get; set; } = new();
}

public class MaintenanceCreateViewModel
{
    [Required]
    public Guid StationId { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string IssueDescription { get; set; } = string.Empty;
}

public class MaintenanceDetailsViewModel
{
    public Guid CompanyId { get; set; }
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
    public List<MaintenanceStatusHistoryViewModel> StatusHistory { get; set; } = new();
}

public class MaintenanceStatusHistoryViewModel
{
    public DateTime AtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Changes { get; set; } = string.Empty;
}
