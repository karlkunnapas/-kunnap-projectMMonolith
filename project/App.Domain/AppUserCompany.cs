using App.Domain.Identity;

namespace App.Domain;

public class AppUserCompany : BaseEntity
{
    public Guid AppUserId { get; set; }
    public Guid CompanyId { get; set; }

    // Navigation properties
    public AppUser? AppUser { get; set; }
    public Company? Company { get; set; }
}