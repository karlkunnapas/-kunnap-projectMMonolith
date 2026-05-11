namespace Modules.Companies.Domain;

internal sealed class Promotion
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CompanyId { get; set; }

    public Company? Company { get; set; }
    public ICollection<UserPromotion>? UserPromotions { get; set; }
}
