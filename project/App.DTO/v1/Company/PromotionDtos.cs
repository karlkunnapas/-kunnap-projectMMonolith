using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Company;

public class PromotionResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
}

public class PromotionUpsert
{
    [Required]
    [MaxLength(64)]
    public string Code { get; set; } = default!;

    [Range(0, 100)]
    public decimal DiscountValue { get; set; }

    [Required]
    public DateTime ValidFromUtc { get; set; }

    [Required]
    public DateTime ValidToUtc { get; set; }

    public bool IsActive { get; set; }
}
