using App.Domain.Identity;

namespace App.Domain;

public class Reservation : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ChargingStationId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public decimal EstimatedCost { get; set; }
    public EReservationStatus Status { get; set; }
    public Guid? PromotionId { get; set; }

    // Navigation properties
    public AppUser? User { get; set; }
    public ChargingStation? ChargingStation { get; set; }
    public Promotion? Promotion { get; set; }
}
