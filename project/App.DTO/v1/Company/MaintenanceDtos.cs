using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Company;

public class MaintenanceIssueResponse
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = default!;
    public string IssueDescription { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime ReportedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string AssignedToUserName { get; set; } = default!;
    public string ReporterUserName { get; set; } = default!;
    public string? Notes { get; set; }
}

public class MaintenanceStatusHistoryResponse
{
    public DateTime AtUtc { get; set; }
    public string Action { get; set; } = default!;
    public string Actor { get; set; } = default!;
    public string Changes { get; set; } = default!;
}

public class MaintenanceStatusUpdate
{
    [Required]
    public int Status { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class MaintenanceAssignment
{
    public Guid? AssignedToUserId { get; set; }
}
