using App.Domain;

namespace App.BLL.DTOs;

public class VehicleCreateDto
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public List<Guid> ConnectorIds { get; set; } = new();
}

public class VehicleUpdateDto
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public List<Guid> ConnectorIds { get; set; } = new();
}

public class VehicleDto
{
    public Guid Id { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public List<VehicleConnectorDto> CompatibleConnectors { get; set; } = new();
}

public class VehicleConnectorDto
{
    public Guid ConnectorId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CompatibleStationDto
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EStationStatus Status { get; set; }
    public List<string> CompatibleConnectorNames { get; set; } = new();
}

