namespace App.DTO.v1.Session;

public class SessionResponse
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = default!;
    public Guid? ReservationId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public decimal EnergyConsumedKwh { get; set; }
    public decimal Cost { get; set; }
    public decimal BaseCostBeforeDiscount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromotionCode { get; set; }
    public bool IsActive { get; set; }
}

public class SessionDetailResponse : SessionResponse
{
    public int DurationMinutes { get; set; }
}
