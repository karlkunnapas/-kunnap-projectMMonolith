namespace WebApp.Areas.Admin.ViewModels;

public class AdminAuditLogListViewModel
{
    public AdminAuditLogFilterViewModel Filter { get; set; } = new();
    public List<AdminAuditLogListItemViewModel> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}
