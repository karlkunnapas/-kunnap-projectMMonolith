using System.ComponentModel.DataAnnotations;

namespace WebApp.Areas.Root.ViewModels;

public class ChargingSessionStartViewModel
{
    [Required]
    public Guid StationId { get; set; }

    [Required]
    public Guid? ReservationId { get; set; }

    public string StationName { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public int EstimatedDurationMinutes { get; set; }
}

public class ChargingSessionActiveViewModel
{
    public Guid Id { get; set; }
    public string StationName { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public decimal CurrentCost { get; set; }
    public decimal EnergyConsumedKwh { get; set; }
}

public class ChargingSessionStopViewModel
{
    [Required]
    public Guid Id { get; set; }

    [StringLength(128)]
    public string? PromotionCode { get; set; }
}

public class ChargingSessionDetailViewModel
{
    public Guid Id { get; set; }
    public string StationName { get; set; } = string.Empty;
    public Guid? ReservationId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public int DurationMinutes { get; set; }
    public decimal EnergyConsumedKwh { get; set; }
    public decimal Cost { get; set; }
    public decimal BaseCostBeforeDiscount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public bool IsActive { get; set; }
    public string? PromotionCode { get; set; }
    public List<PromotionSelectOptionViewModel> AvailablePromotions { get; set; } = new();
}

public class ChargingSessionListViewModel
{
    public List<ChargingSessionDetailViewModel> Sessions { get; set; } = new();
}
