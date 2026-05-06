namespace WebApp.Areas.Admin.ViewModels;

public class AdminStationListViewModel
{
    public string? Search { get; set; }
    public List<AdminStationListItemViewModel> Items { get; set; } = new();
}
