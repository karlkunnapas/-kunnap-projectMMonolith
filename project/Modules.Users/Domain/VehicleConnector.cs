namespace Modules.Users.Domain;

internal sealed class VehicleConnector
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VehicleId { get; set; }
    public Guid ConnectorId { get; set; }

    public Vehicle? Vehicle { get; set; }
}
