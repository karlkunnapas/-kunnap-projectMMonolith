namespace WebApp.Areas.Admin.ViewModels;

public class AdminCompanyListViewModel
{
    public string? Search { get; set; }
    public List<AdminCompanyListItemViewModel> Items { get; set; } = new();
}
