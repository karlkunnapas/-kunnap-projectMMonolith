using System.ComponentModel.DataAnnotations;

namespace WebApp.Areas.Root.ViewModels;

public class VehicleListViewModel
{
    public List<VehicleListItemViewModel> Vehicles { get; set; } = new();
}

public class VehicleListItemViewModel
{
    public Guid Id { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public int CompatibleConnectorCount { get; set; }
}

public class VehicleEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Make { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Model { get; set; } = string.Empty;

    [Range(0, 500)]
    public decimal? BatteryCapacity { get; set; }

    public List<Guid> SelectedConnectorIds { get; set; } = new();
    public List<ConnectorOptionViewModel> AllConnectors { get; set; } = new();
}

public class ConnectorOptionViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

