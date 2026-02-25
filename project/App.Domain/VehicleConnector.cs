namespace App.Domain;

public class VehicleConnector : BaseEntity
{
    public Guid VehicleId { get; set; }
    public Guid ConnectorId { get; set; }

    // Navigation properties
    public Vehicle? Vehicle { get; set; }
    public Connector? Connector { get; set; }
}