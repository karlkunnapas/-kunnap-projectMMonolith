using App.Domain.Identity;

namespace App.Domain;

public class AppUserRole : BaseEntity
{
    public Guid AppUserId { get; set; }
    public Guid AppRoleId { get; set; }

    // Navigation properties
    public AppUser? AppUser { get; set; }
    public AppRole? AppRole { get; set; }
}