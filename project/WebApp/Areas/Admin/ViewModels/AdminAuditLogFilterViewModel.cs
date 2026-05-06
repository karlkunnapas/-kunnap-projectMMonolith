using System.ComponentModel.DataAnnotations;

namespace WebApp.Areas.Admin.ViewModels;

public class AdminAuditLogFilterViewModel
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }

    [StringLength(128)]
    public string? EntityName { get; set; }

    [StringLength(100)]
    public string? Action { get; set; }

    [StringLength(256)]
    public string? Actor { get; set; }

    public string? EntityId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
