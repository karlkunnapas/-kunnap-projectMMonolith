namespace App.Domain;

public class Promotion : BaseEntity
{
    public string Code { get; set; } = default!;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CompanyId { get; set; }

    // Navigation properties
    public Company? Company { get; set; }
    public ICollection<UserPromotion>? UserPromotions { get; set; }
}