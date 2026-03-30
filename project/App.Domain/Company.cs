using System.ComponentModel.DataAnnotations;

namespace App.Domain;

public class Company : BaseEntity
{
    public LangStr Name { get; set; } = default!;
    
    [StringLength(128, MinimumLength = 1)]
    public string ContactEmail { get; set; } = default!;
    [StringLength(128, MinimumLength = 1)]
    public string ContactPhone { get; set; } = default!;
    [StringLength(128, MinimumLength = 1)]
    public string Slug { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<ChargingStation>? ChargingStations { get; set; }
    public ICollection<AppUserCompany>? UserCompanies { get; set; }
    public ICollection<Promotion>? Promotions { get; set; }
    public ICollection<AuditLog>? AuditLogs { get; set; }
}