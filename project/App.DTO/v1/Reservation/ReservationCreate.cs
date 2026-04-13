using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Reservation;

public class ReservationCreate
{
    [Required]
    public Guid StationId { get; set; }

    [Required]
    public DateTime StartTimeUtc { get; set; }

    [Required]
    public DateTime EndTimeUtc { get; set; }

    public decimal? EstimatedEnergyKwh { get; set; }

    [MaxLength(64)]
    public string? PromotionCode { get; set; }
}
