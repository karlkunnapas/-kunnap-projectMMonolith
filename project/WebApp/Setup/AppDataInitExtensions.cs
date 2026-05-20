using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Charging;
using Modules.Charging.Infrastructure;
using Modules.Companies;
using Modules.Companies.Infrastructure;
using Modules.Users;
using Modules.Users.Domain;
using Modules.Users.Infrastructure;

namespace WebApp.Setup;

public static class AppDataInitExtensions
{
    public static void SetupAppData(this WebApplication app)
    {
        using var serviceScope = app.Services
            .GetRequiredService<IServiceScopeFactory>()
            .CreateScope();
        var logger = serviceScope.ServiceProvider.GetRequiredService<ILogger<IApplicationBuilder>>();

        using var usersDb = serviceScope.ServiceProvider.GetRequiredService<UsersDbContext>();

        if (usersDb.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            return;
        }

        WaitDbConnection(usersDb, logger);

        using var userManager = serviceScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var configuration = app.Configuration;

        if (configuration.GetValue<bool>("DataInitialization:DropDatabase"))
        {
            logger.LogWarning("DropDatabase");
            usersDb.Database.EnsureDeleted();
        }

        if (configuration.GetValue<bool>("DataInitialization:MigrateDatabase"))
        {
            logger.LogInformation("MigrateDatabase");
            app.Services.MigrateUsersModuleAsync()
                .GetAwaiter()
                .GetResult();
            app.Services.MigrateCompaniesModuleAsync()
                .GetAwaiter()
                .GetResult();
            app.Services.MigrateChargingModuleAsync()
                .GetAwaiter()
                .GetResult();
        }

        if (configuration.GetValue<bool>("DataInitialization:SeedIdentity"))
        {
            logger.LogInformation("SeedIdentity");
            app.Services.SeedUsersModuleAsync()
                .GetAwaiter()
                .GetResult();
        }

        if (configuration.GetValue<bool>("DataInitialization:SeedData"))
        {
            logger.LogInformation("SeedData");
            var owner = userManager.FindByEmailAsync(CompaniesModuleExtensions.SeedCompanyOwnerEmail)
                .GetAwaiter().GetResult();

            if (owner == null)
            {
                throw new ApplicationException(
                    $"Seed owner user '{CompaniesModuleExtensions.SeedCompanyOwnerEmail}' was not found.");
            }

            var companyId = app.Services
                .SeedCompaniesModuleAsync(owner.Id)
                .GetAwaiter()
                .GetResult();

            app.Services
                .SeedChargingModuleAsync(companyId)
                .GetAwaiter()
                .GetResult();
        }
    }

    private static void WaitDbConnection(UsersDbContext ctx, ILogger<IApplicationBuilder> logger)
    {
        // TODO: Login failed for user 'sa'. Reason: Failed to open the explicitly specified database 'XYZ'. [CLIENT: 172.18.0.3]
        // could actually log in, but db was not there - migrations where not applied yet

        // maybe Database.OpenConnection

        while (true)
        {
            try
            {
                ctx.Database.OpenConnection();
                ctx.Database.CloseConnection();
                return;
            }
            /*
            catch (SqlException e)
            {
                // db server is not yet up
                // A network-related or instance-specific error occurred while establishing a connection to SQL Server. The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections. (provider: TCP Provider, error: 40 - Could not open a connection to SQL Server)
                // its up, but database is not there - apply migration
                // Cannot open database "XYZ" requested by the login. The login failed. Login failed for user 'sa'.

                logger.LogWarning("Checked db connection. Got: {}", e.Message);
                if (e.Message.Contains("The login failed."))
                {
                    logger.LogWarning("Applying migration, probably db is not there (but server is)");
                    return;
                }

                logger.LogWarning("Waiting for db connection. Sleep 1 sec");
                System.Threading.Thread.Sleep(1000);
            }
            */
            catch (Npgsql.PostgresException e)
            {
                logger.LogWarning("Checked postgres db connection. Got: {}", e.Message);

                if (e.Message.Contains("does not exist"))
                {
                    logger.LogWarning("Applying migration, probably db is not there (but server is)");
                    return;
                }

                logger.LogWarning("Waiting for db connection. Sleep 1 sec");
                Thread.Sleep(1000);
            }
        }
    }
}
