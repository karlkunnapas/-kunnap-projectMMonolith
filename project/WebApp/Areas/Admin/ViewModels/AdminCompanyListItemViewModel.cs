namespace WebApp.Areas.Admin.ViewModels;

public class AdminCompanyListItemViewModel
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ActiveMemberCount { get; set; }
}
