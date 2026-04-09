using App.Domain;

namespace App.BLL.DTOs;

public class StationDetailsDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EStationStatus Status { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public List<ConnectorDetailDto> Connectors { get; set; } = new();
    public List<StationReservationDto> ExistingReservations { get; set; } = new();
    public List<AvailabilitySlotDto> AvailableSlots { get; set; } = new();
}

public class StationReservationDto
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public EReservationStatus Status { get; set; }
}

public class ConnectorDetailDto
{
    public Guid ConnectorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int AvailableQuantity { get; set; }
    public List<ReservedTimeRangeDto> Reservations { get; set; } = new();
}

public class ReservedTimeRangeDto
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
}

public class AvailabilitySlotDto
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public bool IsAvailable { get; set; }
}

public class ReservationCreateDto
{
    public Guid StationId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public decimal? EstimatedEnergyKwh { get; set; }
    public string? PromotionCode { get; set; }
}

public class ReservationDto
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public EReservationStatus Status { get; set; }
    public decimal EstimatedCost { get; set; }
}

public class CostEstimateDto
{
    public decimal EstimatedCost { get; set; }
    public int DurationMinutes { get; set; }
}


