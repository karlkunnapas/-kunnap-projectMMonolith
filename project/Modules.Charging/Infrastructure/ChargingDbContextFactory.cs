using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modules.Charging.Infrastructure;

internal sealed class ChargingDbContextFactory : IDesignTimeDbContextFactory<ChargingDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=monolith-project;Username=postgres;Password=postgres";

    public ChargingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ChargingDbContext>()
            .UseNpgsql(
                ResolveConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__migrations", "charging"))
            .Options;

        return new ChargingDbContext(options);
    }

    private static string ResolveConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        return string.IsNullOrWhiteSpace(configured) ? DefaultConnectionString : configured;
    }
}
