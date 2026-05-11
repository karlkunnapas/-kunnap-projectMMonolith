namespace Shared.Contracts.Companies;

public sealed class CreateCompanyWithOwnerMembershipContract
{
    public Guid OwnerUserId { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanySlug { get; set; } = string.Empty;
}

public sealed class CreateCompanyWithOwnerMembershipResultContract
{
    public bool Success { get; set; }
    public Guid CompanyId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static CreateCompanyWithOwnerMembershipResultContract Ok(Guid companyId) => new()
    {
        Success = true,
        CompanyId = companyId
    };

    public static CreateCompanyWithOwnerMembershipResultContract Fail(string errorCode, string errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
