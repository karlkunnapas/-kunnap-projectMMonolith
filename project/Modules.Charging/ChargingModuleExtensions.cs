using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Charging.Application;
using Modules.Charging.Application.Services;
using Modules.Charging.Infrastructure;
using Modules.Charging.Infrastructure.Repositories;
using Modules.Charging.Infrastructure.Seeding;
using Shared.Contracts.Charging;

namespace Modules.Charging;

public static class ChargingModuleExtensions
{
    public static IServiceCollection AddChargingModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ChargingDbContext>(options => options
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__migrations", "charging"))
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging());

        services.AddScoped<IChargingStationRepository, ChargingStationRepository>();
        services.AddScoped<IConnectorRepository, ConnectorRepository>();
        services.AddScoped<IChargingStationConnectorRepository, ChargingStationConnectorRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IChargingSessionRepository, ChargingSessionRepository>();
        services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
        services.AddScoped<IChargingUnitOfWork, ChargingUnitOfWork>();
        services.AddScoped<IChargingRepository, ChargingRepository>();
        services.AddScoped<IChargingApplicationService, ChargingApplicationService>();
        services.AddScoped<IChargingModuleApi, ChargingModuleApi>();
        return services;
    }

    public static async Task MigrateChargingModuleAsync(this IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ChargingDbContext>();
        await db.Database.MigrateAsync(ct);
    }

    public static async Task SeedChargingModuleAsync(
        this IServiceProvider serviceProvider,
        Guid companyId,
        CancellationToken ct = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ChargingDbContext>();
        await ChargingModuleDataSeeder.SeedDataAsync(db, companyId, ct);
    }
}
