using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Modules.Users.Application;
using Modules.Users.Application.Services;
using Modules.Users.Infrastructure;
using Shared.Contracts.Users;

namespace Modules.Users;

public static class UsersModuleExtensions
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<UsersDbContext>(options => options
            .UseNpgsql(connectionString)
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging());

        services
            .AddIdentityCore<Domain.AppUser>(options => options.SignIn.RequireConfirmedAccount = false)
            .AddRoles<Domain.AppRole>()
            .AddEntityFrameworkStores<UsersDbContext>();

        services.AddScoped<IPasswordHasher<Domain.AppUser>, PasswordHasher<Domain.AppUser>>();
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IUsersApplicationService, UsersApplicationService>();
        services.AddScoped<IUsersModuleApi, UsersModuleApi>();
        return services;
    }
}
