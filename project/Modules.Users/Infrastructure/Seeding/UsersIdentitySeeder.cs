using Microsoft.AspNetCore.Identity;

namespace Modules.Users.Infrastructure.Seeding;

internal static class UsersIdentitySeeder
{
    private static readonly string[] Roles =
    {
        "Admin",
        "CompanyOwner",
        "Customer",
        "MaintenancePersonnel",
        "root"
    };

    private static readonly (string Email, string Password, string[] Roles)[] Users =
    {
        ("karl@karl.com", "Karl.123", new[] { "Customer" }),
        ("owner@seed.com", "Owner.123", new[] { "CompanyOwner" }),
        ("admin@admin.com", "Admin.123", new[] { "Admin" })
    };

    internal static async Task SeedIdentityAsync<TUser, TRole>(
        UserManager<TUser> userManager,
        RoleManager<TRole> roleManager,
        CancellationToken ct = default)
        where TUser : IdentityUser<Guid>, new()
        where TRole : IdentityRole<Guid>, new()
    {
        foreach (var roleName in Roles)
        {
            ct.ThrowIfCancellationRequested();
            var role = await roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                continue;
            }

            var createRoleResult = await roleManager.CreateAsync(new TRole
            {
                Name = roleName
            });

            if (!createRoleResult.Succeeded)
            {
                var message = string.Join("; ", createRoleResult.Errors.Select(e => e.Description));
                throw new ApplicationException($"Role creation failed for '{roleName}': {message}");
            }
        }

        foreach (var seedUser in Users)
        {
            ct.ThrowIfCancellationRequested();
            var user = await userManager.FindByEmailAsync(seedUser.Email);
            if (user == null)
            {
                user = new TUser
                {
                    Email = seedUser.Email,
                    UserName = seedUser.Email,
                    EmailConfirmed = true
                };

                var createUserResult = await userManager.CreateAsync(user, seedUser.Password);
                if (!createUserResult.Succeeded)
                {
                    var message = string.Join("; ", createUserResult.Errors.Select(e => e.Description));
                    throw new ApplicationException($"User creation failed for '{seedUser.Email}': {message}");
                }
            }

            foreach (var roleName in seedUser.Roles)
            {
                if (await userManager.IsInRoleAsync(user, roleName))
                {
                    continue;
                }

                var addRoleResult = await userManager.AddToRoleAsync(user, roleName);
                if (!addRoleResult.Succeeded)
                {
                    var message = string.Join("; ", addRoleResult.Errors.Select(e => e.Description));
                    throw new ApplicationException(
                        $"Failed adding user '{seedUser.Email}' to role '{roleName}': {message}");
                }
            }
        }
    }
}
