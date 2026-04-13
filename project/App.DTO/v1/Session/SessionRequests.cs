using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Session;

public class SessionStartRequest
{
    [Required]
    public Guid StationId { get; set; }
    public Guid? ReservationId { get; set; }
}

public class SessionStopRequest
{
    [MaxLength(64)]
    public string? PromotionCode { get; set; }
}
