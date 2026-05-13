namespace Modules.Charging.Domain;

internal sealed class Connector
{
    public Guid Id { get; set; }
    public string NameJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;

    public ICollection<ChargingStationConnector>? ChargingStationConnectors { get; set; }
}
