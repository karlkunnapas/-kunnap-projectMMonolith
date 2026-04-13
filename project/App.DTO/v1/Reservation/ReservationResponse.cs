using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Reservation;

public class ReservationResponse
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = default!;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string Status { get; set; } = default!;
    public decimal EstimatedCost { get; set; }
}

public class RedeemPromotionRequest
{
    [Required]
    [MaxLength(64)]
    public string Code { get; set; } = default!;
}

public class UserPromotionResponse
{
    public Guid Id { get; set; }
    public Guid PromotionId { get; set; }
    public string Code { get; set; } = default!;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsUsed { get; set; }
}
