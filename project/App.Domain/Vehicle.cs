using System.ComponentModel.DataAnnotations;
using App.Domain.Identity;

namespace App.Domain;

public class Vehicle : BaseEntity
{
    public Guid UserId { get; set; }
    [StringLength(128, MinimumLength = 1)]
    public string Make { get; set; } = default!;
    [StringLength(128, MinimumLength = 1)]
    public string Model { get; set; } = default!;
    public decimal? BatteryCapacity { get; set; }

    // Navigation properties
    public AppUser? User { get; set; }
    public ICollection<VehicleConnector>? VehicleConnectors { get; set; }
}