using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modules.Users.Infrastructure;

internal sealed class UsersDbContextFactory : IDesignTimeDbContextFactory<UsersDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=monolith-project;Username=postgres;Password=postgres";

    public UsersDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseNpgsql(
                ResolveConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__migrations", "users"))
            .Options;

        return new UsersDbContext(options);
    }

    private static string ResolveConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        return string.IsNullOrWhiteSpace(configured) ? DefaultConnectionString : configured;
    }
}
