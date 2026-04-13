using System.ComponentModel.DataAnnotations;

namespace WebApp.Areas.Root.ViewModels;

public class PromotionWalletViewModel
{
    [StringLength(128)]
    public string RedeemCode { get; set; } = string.Empty;
    public List<PromotionWalletItemViewModel> Promotions { get; set; } = new();
}

public class PromotionWalletItemViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public bool IsActive { get; set; }
}
