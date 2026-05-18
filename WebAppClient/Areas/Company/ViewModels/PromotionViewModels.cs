using System.ComponentModel.DataAnnotations;

namespace WebApp.Areas.Company.ViewModels;

public class CompanyPromotionListViewModel
{
    public Guid CompanyId { get; set; }
    public List<CompanyPromotionItemViewModel> Promotions { get; set; } = new();
}

public class CompanyPromotionItemViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
    public bool CanDelete { get; set; }
}

public class CompanyPromotionFormViewModel
{
    public Guid CompanyId { get; set; }
    public Guid? Id { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Range(0.01, 99.99)]
    public decimal DiscountValue { get; set; }

    [Required]
    public DateTime ValidFromUtc { get; set; }

    [Required]
    public DateTime ValidToUtc { get; set; }

    public bool IsActive { get; set; } = true;
}
