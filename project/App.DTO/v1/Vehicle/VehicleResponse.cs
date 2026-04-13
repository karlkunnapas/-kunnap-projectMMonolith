namespace App.DTO.v1.Vehicle;

public class VehicleResponse
{
    public Guid Id { get; set; }
    public string Make { get; set; } = default!;
    public string Model { get; set; } = default!;
    public decimal? BatteryCapacity { get; set; }
    public List<VehicleConnectorResponse> CompatibleConnectors { get; set; } = new();
}

public class VehicleConnectorResponse
{
    public Guid ConnectorId { get; set; }
    public string Name { get; set; } = default!;
}
