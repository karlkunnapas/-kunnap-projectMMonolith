namespace Modules.Charging.Domain;

internal sealed class Reservation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ChargingStationId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public decimal EstimatedCost { get; set; }
    public EReservationStatus Status { get; set; }
    public Guid? PromotionId { get; set; }

    public ChargingStation? ChargingStation { get; set; }
}
