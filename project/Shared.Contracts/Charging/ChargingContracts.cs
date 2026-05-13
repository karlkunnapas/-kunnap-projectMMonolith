namespace Shared.Contracts.Charging;

public sealed class ChargingStationContract
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EStationStatus Status { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public bool IsActive { get; set; }
    public Guid? CompanyId { get; set; }
    public IReadOnlyCollection<ConnectorContract> Connectors { get; set; } = Array.Empty<ConnectorContract>();
}

public sealed class ConnectorContract
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class AdminChargingStationContract
{
    public Guid StationId { get; set; }
    public Guid? CompanyId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameEt { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EStationStatus Status { get; set; }
    public bool IsActive { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
}

public sealed class ConnectorTypeContract
{
    public Guid ConnectorTypeId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameEt { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ReservationContract
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ChargingStationId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public decimal EstimatedCost { get; set; }
    public EReservationStatus Status { get; set; }
    public Guid? PromotionId { get; set; }
    public string StationName { get; set; } = string.Empty;
}

public sealed class ChargingSessionContract
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ChargingStationId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? PromotionId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public decimal EnergyConsumed { get; set; }
    public decimal Cost { get; set; }
    public string StationName { get; set; } = string.Empty;
    public decimal StationPricePerKwh { get; set; }
    public decimal? StationMaxPower { get; set; }
    public string? PromotionCode { get; set; }
    public decimal? PromotionDiscountValue { get; set; }
}

public sealed class MaintenanceContract
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid ChargingStationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public Guid? ReportedByUserId { get; set; }
    public string IssueDescription { get; set; } = string.Empty;
    public EMaintenanceStatus Status { get; set; }
    public DateTime ReportedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? Notes { get; set; }
}

public sealed class CompanyDashboardStatsContract
{
    public int TotalStations { get; set; }
    public int AvailableStations { get; set; }
    public int InUseStations { get; set; }
    public int MaintenanceStations { get; set; }
    public int ActiveReservations { get; set; }
    public int ActiveSessions { get; set; }
    public decimal RevenueTotal { get; set; }
}

public sealed class UpsertCompanyStationContract
{
    public Guid? StationId { get; set; }
    public Guid CompanyId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameEt { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EStationStatus Status { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public bool IsActive { get; set; }
}
