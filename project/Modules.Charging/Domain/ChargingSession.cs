namespace Modules.Charging.Domain;

internal sealed class ChargingSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ChargingStationId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? PromotionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal EnergyConsumed { get; set; }
    public decimal Cost { get; set; }

    public ChargingStation? ChargingStation { get; set; }
    public Reservation? Reservation { get; set; }
}
