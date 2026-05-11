namespace Shared.Contracts.Companies;

public sealed class UserCompanyMembershipContract
{
    public Guid MembershipId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
