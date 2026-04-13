namespace WebApp.ViewModels;

public class AuditEntryViewModel
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public DateTime AtUtc { get; set; }
    public string ChangesSummary { get; set; } = string.Empty;
}

public class AuditTrailViewModel
{
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public List<AuditEntryViewModel> Entries { get; set; } = new();
}

public class CompanyAuditViewModel
{
    public Guid CompanyId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? EntityName { get; set; }
    public string? Action { get; set; }
    public List<AuditEntryViewModel> Entries { get; set; } = new();
}

