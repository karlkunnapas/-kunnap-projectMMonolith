using Microsoft.AspNetCore.Identity;

namespace Modules.Users.Domain;

internal sealed class AppUser : IdentityUser<Guid>
{
}
