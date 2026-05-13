namespace Modules.Charging.Domain;

internal sealed class ChargingStationConnector
{
    public Guid Id { get; set; }
    public Guid ChargingStationId { get; set; }
    public Guid ConnectorId { get; set; }

    public ChargingStation? ChargingStation { get; set; }
    public Connector? Connector { get; set; }
}
