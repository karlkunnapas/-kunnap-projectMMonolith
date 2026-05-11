using System.ComponentModel.DataAnnotations;

namespace Modules.Users.Domain;

internal sealed class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    [StringLength(128, MinimumLength = 1)]
    public string Make { get; set; } = default!;

    [StringLength(128, MinimumLength = 1)]
    public string Model { get; set; } = default!;

    public decimal? BatteryCapacity { get; set; }

    public ICollection<VehicleConnector>? VehicleConnectors { get; set; }
}
