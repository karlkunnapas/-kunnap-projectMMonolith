using Microsoft.AspNetCore.Identity;

namespace Modules.Users.Domain;

public sealed class AppUser : IdentityUser<Guid>
{
}
