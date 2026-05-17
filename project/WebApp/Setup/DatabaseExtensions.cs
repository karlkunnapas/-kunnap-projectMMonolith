using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Users.Infrastructure;
using Npgsql;
using Shared.Contracts.Auditing;
using WebApp.Helpers;

namespace WebApp.Setup;

public static class DatabaseExtensions
{
    public static IServiceCollection AddAppDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _ = configuration;
        _ = environment;

        // used for older style [Column(TypeName = "jsonb")] for LangStr
#pragma warning disable CS0618 // Type or member is obsolete
        NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();
#pragma warning restore CS0618 // Type or member is obsolete

        services.AddDatabaseDeveloperPageExceptionFilter();
        services.AddDataProtection().PersistKeysToDbContext<UsersDbContext>();
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditActorProvider, HttpContextAuditActorProvider>();

        return services;
    }
}
