namespace WebApp.Areas.Admin.ViewModels;

public class AdminConnectorTypeListItemViewModel
{
    public Guid ConnectorTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
