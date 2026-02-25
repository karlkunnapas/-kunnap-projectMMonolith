namespace App.Domain;

public class Connector : BaseEntity
{
    public LangStr Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<ChargingStationConnector>? ChargingStationConnectors { get; set; }
    public ICollection<VehicleConnector>? VehicleConnectors { get; set; }
}