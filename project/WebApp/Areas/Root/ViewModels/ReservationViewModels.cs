using System.ComponentModel.DataAnnotations;
using Shared.Contracts.Charging;

namespace WebApp.Areas.Root.ViewModels;

public class StationDetailsViewModel
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EStationStatus Status { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public List<ConnectorDetailViewModel> Connectors { get; set; } = new();
    public List<StationReservationViewModel> ExistingReservations { get; set; } = new();
    public List<AvailabilitySlotViewModel> AvailableSlots { get; set; } = new();
    public List<VehicleOptionViewModel> Vehicles { get; set; } = new();
    public Guid? SelectedVehicleId { get; set; }
    public Guid? SelectedConnectorId { get; set; }
    public int CurrentBatteryPercent { get; set; } = 25;
    public int DesiredBatteryPercent { get; set; } = 80;
    public decimal CalculatedEnergyKwh { get; set; }
    public int CalculatedDurationMinutes { get; set; }
    public decimal CalculatedCost { get; set; }
    public ReservationCreateViewModel ReservationForm { get; set; } = new();
    public StationIssueReportViewModel IssueReportForm { get; set; } = new();
}

public class StationIssueReportViewModel
{
    public Guid StationId { get; set; }

    [Required]
    [StringLength(256, MinimumLength = 5)]
    public string IssueDescription { get; set; } = string.Empty;
}

public class StationReservationViewModel
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public EReservationStatus Status { get; set; }
}

public class ConnectorDetailViewModel
{
    public Guid ConnectorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int AvailableQuantity { get; set; }
    public List<ReservedTimeRangeViewModel> Reservations { get; set; } = new();
}

public class ReservedTimeRangeViewModel
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
}

public class AvailabilitySlotViewModel
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public bool IsAvailable { get; set; }
}

public class VehicleOptionViewModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal BatteryCapacityKwh { get; set; }
    public bool IsSelected { get; set; }
}

public class PromotionSelectOptionViewModel
{
    public string Code { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
}

public class ReservationCreateViewModel
{
    public Guid StationId { get; set; }

    [Required]
    public DateTime StartTimeUtc { get; set; }

    [Required]
    public DateTime EndTimeUtc { get; set; }

    [Range(0, 1000)]
    public decimal? EstimatedEnergyKwh { get; set; }

    [StringLength(128)]
    public string? PromotionCode { get; set; }
    public List<PromotionSelectOptionViewModel> AvailablePromotions { get; set; } = new();

    public decimal EstimatedCost { get; set; }
    public string StationName { get; set; } = string.Empty;
    public bool CanReserve { get; set; } = true;
}

public class ReservationListViewModel
{
    public List<ReservationListItemViewModel> Reservations { get; set; } = new();
}

public class ReservationListItemViewModel
{
    public Guid Id { get; set; }
    public string StationName { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public EReservationStatus Status { get; set; }
    public decimal EstimatedCost { get; set; }
    public bool CanStart { get; set; }
}

public class ReservationDetailViewModel
{
    public Guid Id { get; set; }
    public string StationName { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public EReservationStatus Status { get; set; }
    public decimal EstimatedCost { get; set; }
}
