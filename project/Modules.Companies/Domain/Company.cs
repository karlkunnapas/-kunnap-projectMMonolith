namespace Modules.Companies.Domain;

internal sealed class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "{}";
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<AppUserCompany>? UserCompanies { get; set; }
    public ICollection<Promotion>? Promotions { get; set; }
    public ICollection<AuditLog>? AuditLogs { get; set; }
}
