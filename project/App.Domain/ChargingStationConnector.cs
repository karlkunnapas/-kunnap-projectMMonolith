namespace App.Domain;

public class ChargingStationConnector : BaseEntity
{
    public Guid ChargingStationId { get; set; }
    public Guid ConnectorId { get; set; }

    // Navigation properties
    public ChargingStation? ChargingStation { get; set; }
    public Connector? Connector { get; set; }
}