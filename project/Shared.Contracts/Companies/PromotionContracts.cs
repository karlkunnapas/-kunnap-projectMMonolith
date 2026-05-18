namespace Shared.Contracts.Companies;

public sealed class CompanyPromotionContract
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
    public bool CanDelete { get; set; } = true;
    public Guid? CompanyId { get; set; }
}

public sealed class UpsertCompanyPromotionContract
{
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
}

public sealed class UserPromotionContract
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PromotionId { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public bool IsUsed { get; set; }
    public CompanyPromotionContract? Promotion { get; set; }
}

public sealed class PromotionOperationResultContract
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public CompanyPromotionContract? Promotion { get; set; }

    public static PromotionOperationResultContract Ok(CompanyPromotionContract promotion) => new()
    {
        Success = true,
        Promotion = promotion
    };

    public static PromotionOperationResultContract Fail(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        ErrorMessage = message
    };
}
