using System.ComponentModel.DataAnnotations;

namespace Modules.Charging.Domain;

internal sealed class Maintenance
{
    public Guid Id { get; set; }
    public Guid ChargingStationId { get; set; }
    public Guid? ReportedByUserId { get; set; }

    [StringLength(128, MinimumLength = 1)]
    public string IssueDescription { get; set; } = string.Empty;

    public EMaintenanceStatus Status { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public Guid? AssignedToUserId { get; set; }

    [StringLength(128, MinimumLength = 1)]
    public string? Notes { get; set; }

    public ChargingStation? ChargingStation { get; set; }
}
