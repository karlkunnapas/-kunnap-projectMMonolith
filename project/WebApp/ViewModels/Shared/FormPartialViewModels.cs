namespace WebApp.ViewModels.Shared;

public class BilingualInputViewModel
{
    public string NameEnLabel { get; set; } = string.Empty;
    public string NameEtLabel { get; set; } = string.Empty;
    public string NameEnFieldName { get; set; } = string.Empty;
    public string NameEtFieldName { get; set; } = string.Empty;
    public string NameEnValue { get; set; } = string.Empty;
    public string NameEtValue { get; set; } = string.Empty;
    public string NameEnPlaceholder { get; set; } = string.Empty;
    public string NameEtPlaceholder { get; set; } = string.Empty;
}

public class ConnectorChecklistViewModel
{
    public string Heading { get; set; } = string.Empty;
    public string InputName { get; set; } = string.Empty;
    public IReadOnlyCollection<ConnectorChecklistItemViewModel> Items { get; set; } = Array.Empty<ConnectorChecklistItemViewModel>();
}

public class ConnectorChecklistItemViewModel
{
    public Guid ConnectorId { get; set; }
    public string ConnectorName { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
}
