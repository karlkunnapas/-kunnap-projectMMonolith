using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Vehicle;

public class VehicleCreate
{
    [Required]
    [MaxLength(100)]
    public string Make { get; set; } = default!;

    [Required]
    [MaxLength(100)]
    public string Model { get; set; } = default!;

    [Range(1, 500)]
    public decimal? BatteryCapacity { get; set; }
    public List<Guid> ConnectorIds { get; set; } = new();
}

public class VehicleUpdate
{
    [Required]
    [MaxLength(100)]
    public string Make { get; set; } = default!;

    [Required]
    [MaxLength(100)]
    public string Model { get; set; } = default!;

    [Range(1, 500)]
    public decimal? BatteryCapacity { get; set; }
    public List<Guid> ConnectorIds { get; set; } = new();
}
