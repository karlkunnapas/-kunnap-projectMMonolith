namespace WebApp.Areas.Admin.ViewModels;

public class AdminAuditLogListItemViewModel
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
