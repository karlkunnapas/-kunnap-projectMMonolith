namespace Modules.Companies.Domain;

internal sealed class UserPromotion
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PromotionId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public bool IsUsed { get; set; }

    public Promotion? Promotion { get; set; }
}
