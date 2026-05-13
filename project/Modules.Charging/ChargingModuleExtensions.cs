using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Charging.Application;
using Modules.Charging.Infrastructure;
using Shared.Contracts.Charging;

namespace Modules.Charging;

public static class ChargingModuleExtensions
{
    public static IServiceCollection AddChargingModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ChargingDbContext>(options => options
            .UseNpgsql(connectionString)
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging());

        services.AddScoped<IChargingModuleApi, ChargingModuleApi>();
        return services;
    }
}
