using System.ComponentModel.DataAnnotations;

namespace App.Domain;

public class ChargingStation : BaseEntity
{
    public LangStr Name { get; set; } = default!;
    
    [StringLength(128, MinimumLength = 1)]
    public string Location { get; set; } = default!;
    public EStationStatus Status { get; set; }
    public decimal PricePerHour { get; set; }
    public decimal MaxPower { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CompanyId { get; set; }

    // Navigation properties
    public Company? Company { get; set; }
    public ICollection<Reservation>? Reservations { get; set; }
    public ICollection<ChargingSession>? ChargingSessions { get; set; }
    public ICollection<Maintenance>? MaintenanceIssues { get; set; }
    public ICollection<ChargingStationConnector>? ChargingStationConnectors { get; set; }
}