namespace WebApp.Areas.Admin.ViewModels;

public class AdminConnectorTypeListViewModel
{
    public string? Search { get; set; }
    public List<AdminConnectorTypeListItemViewModel> Items { get; set; } = new();
}
