using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Modules.Charging.Infrastructure;
using Modules.Companies.Infrastructure;
using Modules.Users.Infrastructure;
using Modules.Users.Infrastructure.Seeding;
using Modules.Users.Domain;
using WebApp.Tests.Helpers;

namespace WebApp.Tests;

public class CustomWebApplicationFactory<TStartup>
    : WebApplicationFactory<TStartup> where TStartup: class
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataInitialization:DropDatabase"] = "false",
                ["DataInitialization:MigrateDatabase"] = "false",
                ["DataInitialization:SeedIdentity"] = "false",
                ["DataInitialization:SeedData"] = "false",
                ["JWT:Key"] = "phase2-test-jwt-key-must-be-at-least-64-bytes-long-abcdefghijklmnopqrstuvwxyz-0123456789",
                ["JWT:Issuer"] = "test-issuer",
                ["JWT:Audience"] = "test-audience"
            });
        });

        builder.ConfigureServices(services =>
        {
            _connection.Open();

            services.RemoveAll<UsersDbContext>();
            services.RemoveAll<CompaniesDbContext>();
            services.RemoveAll<ChargingDbContext>();
            services.RemoveAll<DbContextOptions<UsersDbContext>>();
            services.RemoveAll<DbContextOptions<CompaniesDbContext>>();
            services.RemoveAll<DbContextOptions<ChargingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<UsersDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CompaniesDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ChargingDbContext>>();

            services.AddDbContext<UsersDbContext>(options => options.UseSqlite(_connection));
            services.AddDbContext<CompaniesDbContext>(options => options.UseSqlite(_connection));
            services.AddDbContext<ChargingDbContext>(options => options.UseSqlite(_connection));

            
            // create db and seed data
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var usersDb = scopedServices.GetRequiredService<UsersDbContext>();
            var companiesDb = scopedServices.GetRequiredService<CompaniesDbContext>();
            var chargingDb = scopedServices.GetRequiredService<ChargingDbContext>();
            var logger = scopedServices
                .GetRequiredService<ILogger<CustomWebApplicationFactory<TStartup>>>();

            usersDb.Database.EnsureDeleted();
            usersDb.Database.EnsureCreated();
            companiesDb.GetService<IRelationalDatabaseCreator>().CreateTables();
            chargingDb.GetService<IRelationalDatabaseCreator>().CreateTables();

            try
            {
                var roleManager = scopedServices.GetRequiredService<RoleManager<AppRole>>();
                var userManager = scopedServices.GetRequiredService<UserManager<AppUser>>();
                UsersIdentitySeeder.SeedIdentityAsync(userManager, roleManager).GetAwaiter().GetResult();
                DataSeeder.SeedData(companiesDb, chargingDb, userManager);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred seeding the " +
                                    "database with test data. Error: {Message}", ex.Message);
            }
        });
    }

}
