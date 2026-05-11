namespace Modules.Users.Domain;

internal sealed class AppUserRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppUserId { get; set; }
    public Guid AppRoleId { get; set; }

    public AppUser? AppUser { get; set; }
    public AppRole? AppRole { get; set; }
}
