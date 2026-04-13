using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Station;

public class StationDetails
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public string Name { get; set; } = default!;
    public string Location { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public List<ConnectorDetail> Connectors { get; set; } = new();
    public List<StationReservationSlot> ExistingReservations { get; set; } = new();
    public List<AvailabilitySlot> AvailableSlots { get; set; } = new();
}

public class ConnectorDetail
{
    public Guid ConnectorId { get; set; }
    public string Name { get; set; } = default!;
    public int Quantity { get; set; }
    public int AvailableQuantity { get; set; }
    public List<TimeRange> Reservations { get; set; } = new();
}

public class StationReservationSlot
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public string Status { get; set; } = default!;
}

public class TimeRange
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
}

public class AvailabilitySlot
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public bool IsAvailable { get; set; }
}

public class CostEstimate
{
    public decimal EstimatedCost { get; set; }
    public int DurationMinutes { get; set; }
}

public class ConnectorOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
}

public class ReportIssueRequest
{
    [Required]
    [MaxLength(1000)]
    public string IssueDescription { get; set; } = default!;
}
