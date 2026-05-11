namespace Shared.Contracts.Companies;

public sealed class CompanyAuditEntryContract
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

public sealed class CompanyAuditTrailContract
{
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public List<CompanyAuditEntryContract> Entries { get; set; } = new();
}
