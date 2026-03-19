using System.ComponentModel.DataAnnotations;
using App.Domain.Identity;

namespace App.Domain;

public class Maintenance : BaseEntity
{
    public Guid ChargingStationId { get; set; }
    public Guid? ReportedByUserId { get; set; }
    
    [StringLength(128, MinimumLength = 1)]
    public string IssueDescription { get; set; } = default!;
    public EMaintenanceStatus Status { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public Guid? AssignedToUserId { get; set; }
    [StringLength(128, MinimumLength = 1)]
    public string? Notes { get; set; }

    // Navigation properties
    public ChargingStation? ChargingStation { get; set; }
    public AppUser? ReportedByUser { get; set; }
    public AppUser? AssignedToUser { get; set; }
}