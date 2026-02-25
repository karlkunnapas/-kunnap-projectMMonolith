using App.Domain.Identity;

namespace App.Domain;

public class Vehicle : BaseEntity
{
    public Guid UserId { get; set; }
    public string Make { get; set; } = default!;
    public string Model { get; set; } = default!;
    public decimal? BatteryCapacity { get; set; }

    // Navigation properties
    public AppUser? User { get; set; }
    public ICollection<VehicleConnector>? VehicleConnectors { get; set; }
}