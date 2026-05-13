using System;
using System.Linq;
using App.DAL.EF;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Modules.Charging.Infrastructure;
using Modules.Companies.Infrastructure;
using Modules.Users.Infrastructure;
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

            services.RemoveAll<AppDbContext>();
            services.RemoveAll<UsersDbContext>();
            services.RemoveAll<CompaniesDbContext>();
            services.RemoveAll<ChargingDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<DbContextOptions<UsersDbContext>>();
            services.RemoveAll<DbContextOptions<CompaniesDbContext>>();
            services.RemoveAll<DbContextOptions<ChargingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<UsersDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CompaniesDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ChargingDbContext>>();

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.AddDbContext<UsersDbContext>(options => options.UseSqlite(_connection));
            services.AddDbContext<CompaniesDbContext>(options => options.UseSqlite(_connection));
            services.AddDbContext<ChargingDbContext>(options => options.UseSqlite(_connection));

            
            // create db and seed data
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<AppDbContext>();
            var usersDb = scopedServices.GetRequiredService<UsersDbContext>();
            var companiesDb = scopedServices.GetRequiredService<CompaniesDbContext>();
            var chargingDb = scopedServices.GetRequiredService<ChargingDbContext>();
            var logger = scopedServices
                .GetRequiredService<ILogger<CustomWebApplicationFactory<TStartup>>>();

            db.Database.EnsureCreated();
            usersDb.Database.EnsureCreated();
            companiesDb.Database.EnsureCreated();
            chargingDb.Database.EnsureCreated();

            try
            {
                var roleManager = scopedServices.GetRequiredService<RoleManager<AppRole>>();
                var userManager = scopedServices.GetRequiredService<UserManager<AppUser>>();
                SeedUsersModuleIdentity(roleManager, userManager);
                DataSeeder.SeedData(db);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred seeding the " +
                                    "database with test data. Error: {Message}", ex.Message);
            }
        });
    }

    private static void SeedUsersModuleIdentity(RoleManager<AppRole> roleManager, UserManager<AppUser> userManager)
    {
        var roles = new[] { "Admin", "CompanyOwner", "Customer", "MaintenancePersonnel", "root" };
        foreach (var roleName in roles)
        {
            var role = roleManager.FindByNameAsync(roleName).GetAwaiter().GetResult();
            if (role == null)
            {
                roleManager.CreateAsync(new AppRole { Name = roleName }).GetAwaiter().GetResult();
            }
        }
    }
}
