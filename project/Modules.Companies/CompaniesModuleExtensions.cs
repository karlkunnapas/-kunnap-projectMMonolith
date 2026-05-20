using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Companies.Application;
using Modules.Companies.Application.Services;
using Modules.Companies.Infrastructure;
using Modules.Companies.Infrastructure.Seeding;
using Shared.Contracts.Companies;

namespace Modules.Companies;

public static class CompaniesModuleExtensions
{
    public static string SeedCompanyOwnerEmail => CompaniesModuleDataSeeder.SeedCompanyOwnerEmail;

    public static IServiceCollection AddCompaniesModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CompaniesDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__migrations", "companies")));
        services.AddScoped<ICompaniesUnitOfWork, CompaniesUnitOfWork>();
        services.AddScoped<ICompaniesRepository, CompaniesRepository>();
        services.AddScoped<ICompaniesApplicationService, CompaniesApplicationService>();
        services.AddScoped<ICompaniesModuleApi, CompaniesModuleApi>();
        return services;
    }

    public static async Task MigrateCompaniesModuleAsync(this IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CompaniesDbContext>();
        await db.Database.MigrateAsync(ct);
    }

    public static async Task<Guid> SeedCompaniesModuleAsync(
        this IServiceProvider serviceProvider,
        Guid ownerUserId,
        CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CompaniesDbContext>();
        return await CompaniesModuleDataSeeder.SeedCompanyAndOwnerMembershipAsync(db, ownerUserId, ct);
    }
}
