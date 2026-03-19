using System.ComponentModel.DataAnnotations;

namespace App.Domain;

public class AuditLog : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    [StringLength(256, MinimumLength = 1)]
    public string UserName { get; set; } = default!;

    [StringLength(128, MinimumLength = 1)]
    public string EntityName { get; set; } = default!;

    public Guid EntityId { get; set; }

    [MaxLength(100)]
    public string Action { get; set; } = default!;

    public DateTime AtUtc { get; set; }

    [MaxLength(16000)]
    public string? ChangesJson { get; set; }
}
