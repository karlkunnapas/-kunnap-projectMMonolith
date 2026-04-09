using App.Domain.Identity;

namespace App.Domain;

public class ChargingSession : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ChargingStationId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? PromotionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal EnergyConsumed { get; set; }
    public decimal Cost { get; set; }

    // Navigation properties
    public AppUser? User { get; set; }
    public ChargingStation? ChargingStation { get; set; }
    public Reservation? Reservation { get; set; }
    public Promotion? Promotion { get; set; }
}
