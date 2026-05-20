using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modules.Companies.Infrastructure;

internal sealed class CompaniesDbContextFactory : IDesignTimeDbContextFactory<CompaniesDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=monolith-project;Username=postgres;Password=postgres";

    public CompaniesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CompaniesDbContext>()
            .UseNpgsql(
                ResolveConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__migrations", "companies"))
            .Options;

        return new CompaniesDbContext(options);
    }

    private static string ResolveConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        return string.IsNullOrWhiteSpace(configured) ? DefaultConnectionString : configured;
    }
}
