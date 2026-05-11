namespace Shared.Contracts.Companies;

public sealed class CompanyMembershipContract
{
    public Guid MembershipId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}

public sealed class UpsertCompanyMembershipContract
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
}

public sealed class UpsertCompanyMembershipResultContract
{
    public CompanyMembershipContract Membership { get; set; } = new();
    public string Operation { get; set; } = string.Empty;
}
