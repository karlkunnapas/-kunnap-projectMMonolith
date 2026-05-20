using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Modules.Users.Application;
using Modules.Users.Application.Services;
using Modules.Users.Domain;
using Modules.Users.Infrastructure;
using Modules.Users.Infrastructure.Seeding;
using Shared.Contracts.Users;

namespace Modules.Users;

public static class UsersModuleExtensions
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<UsersDbContext>(options => options
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__migrations", "users"))
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging());

        services
            .AddIdentityCore<Domain.AppUser>(options => options.SignIn.RequireConfirmedAccount = false)
            .AddRoles<Domain.AppRole>()
            .AddEntityFrameworkStores<UsersDbContext>();

        services.AddScoped<IPasswordHasher<Domain.AppUser>, PasswordHasher<Domain.AppUser>>();
        services.AddScoped<IUsersUnitOfWork, UsersUnitOfWork>();
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IUsersApplicationService, UsersApplicationService>();
        services.AddScoped<IUsersModuleApi, UsersModuleApi>();
        return services;
    }

    public static async Task MigrateUsersModuleAsync(this IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        await db.Database.MigrateAsync(ct);
    }

    public static async Task SeedUsersModuleAsync(this IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        await UsersIdentitySeeder.SeedIdentityAsync(userManager, roleManager, ct);
    }
}
