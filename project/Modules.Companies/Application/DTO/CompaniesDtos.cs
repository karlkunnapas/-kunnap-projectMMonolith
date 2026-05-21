namespace Modules.Companies.Application.DTO;


internal sealed class AdminCompanyDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ActiveMembersCount { get; set; }
}

internal sealed class CompanyAuditEntryDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }
    public string? ChangesJson { get; set; }
}

internal sealed class CompanyAuditTrailDto
{
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public List<CompanyAuditEntryDto> Entries { get; set; } = new();
}

internal sealed class UserCompanyMembershipDto
{
    public Guid MembershipId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

internal sealed class CompanyMembershipDto
{
    public Guid MembershipId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}

internal sealed class UpsertCompanyMembershipDto
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
}

internal sealed class UpsertCompanyMembershipResultDto
{
    public CompanyMembershipDto Membership { get; set; } = new();
    public string Operation { get; set; } = string.Empty;
}

internal sealed class CompanyPromotionDto
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

internal sealed class UpsertCompanyPromotionDto
{
    public string Code { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; }
}

internal sealed class UserPromotionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PromotionId { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public bool IsUsed { get; set; }
    public CompanyPromotionDto? Promotion { get; set; }
}

internal sealed class PromotionOperationResultDto
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public CompanyPromotionDto? Promotion { get; set; }

    public static PromotionOperationResultDto Ok(CompanyPromotionDto promotion)
    {
        return new()
        {
            Success = true,
            Promotion = promotion
        };
    }

    public static PromotionOperationResultDto Fail(string code, string message)
    {
        return new()
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message
        };
    }
}

internal sealed class CreateCompanyWithOwnerMembershipDto
{
    public Guid OwnerUserId { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanySlug { get; set; } = string.Empty;
}

internal sealed class CreateCompanyWithOwnerMembershipResultDto
{
    public bool Success { get; set; }
    public Guid CompanyId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static CreateCompanyWithOwnerMembershipResultDto Ok(Guid companyId)
    {
        return new()
        {
            Success = true,
            CompanyId = companyId
        };
    }

    public static CreateCompanyWithOwnerMembershipResultDto Fail(string errorCode, string errorMessage)
    {
        return new()
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
