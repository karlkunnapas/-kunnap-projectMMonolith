namespace App.BLL.DTOs;

public class PromotionSummaryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
}

public class PromotionUpsertDto
{
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
}

public class UserPromotionDto
{
    public Guid Id { get; set; }
    public Guid PromotionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsUsed { get; set; }
}

public class AppliedPromotionDto
{
    public Guid PromotionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
}
