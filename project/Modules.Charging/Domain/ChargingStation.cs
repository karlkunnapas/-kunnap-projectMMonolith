using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Modules.Charging.Domain;

internal sealed class ChargingStation
{
    public Guid Id { get; set; }
    public string NameJson { get; set; } = "{}";

    [StringLength(128, MinimumLength = 1)]
    public string Location { get; set; } = string.Empty;

    public EStationStatus Status { get; set; }

    [Column("PricePerHour")]
    public decimal PricePerKwh { get; set; }

    public decimal MaxPower { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CompanyId { get; set; }

    public ICollection<Reservation>? Reservations { get; set; }
    public ICollection<ChargingSession>? ChargingSessions { get; set; }
    public ICollection<Maintenance>? MaintenanceIssues { get; set; }
    public ICollection<ChargingStationConnector>? ChargingStationConnectors { get; set; }
}
