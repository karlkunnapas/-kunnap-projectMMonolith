using System.Threading;
using App.DAL.EF;
using App.DAL.EF.Seeding;
using App.Domain.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WebApp.Setup;

public static class AppDataInitExtensions
{
    public static void SetupAppData(this WebApplication app)
    {
        using var serviceScope = app.Services
            .GetRequiredService<IServiceScopeFactory>()
            .CreateScope();
        var logger = serviceScope.ServiceProvider.GetRequiredService<ILogger<IApplicationBuilder>>();

        using var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory") return;

        WaitDbConnection(context, logger);

        using var userManager = serviceScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        using var roleManager = serviceScope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        var configuration = app.Configuration;

        if (configuration.GetValue<bool>("DataInitialization:DropDatabase"))
        {
            logger.LogWarning("DropDatabase");
            AppDataInit.DeleteDatabase(context);
        }

        if (configuration.GetValue<bool>("DataInitialization:MigrateDatabase"))
        {
            logger.LogInformation("MigrateDatabase");
            AppDataInit.MigrateDatabase(context);
        }

        if (configuration.GetValue<bool>("DataInitialization:SeedIdentity"))
        {
            logger.LogInformation("SeedIdentity");
            AppDataInit.SeedIdentity(userManager, roleManager);
        }

        if (configuration.GetValue<bool>("DataInitialization:SeedData"))
        {
            logger.LogInformation("SeedData");
            AppDataInit.SeedAppData(context);
        }
    }

    private static void WaitDbConnection(AppDbContext ctx, ILogger<IApplicationBuilder> logger)
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