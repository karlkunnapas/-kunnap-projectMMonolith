namespace Shared.Contracts.Companies;

public sealed class AdminCompanyContract
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ActiveMembersCount { get; set; }
}
