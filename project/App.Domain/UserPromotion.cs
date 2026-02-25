using App.Domain.Identity;

namespace App.Domain;

public class UserPromotion : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PromotionId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public AppUser? User { get; set; }
    public Promotion? Promotion { get; set; }
}