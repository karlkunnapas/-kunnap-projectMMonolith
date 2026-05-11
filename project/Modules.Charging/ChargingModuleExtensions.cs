using Microsoft.Extensions.DependencyInjection;

namespace Modules.Charging;

public static class ChargingModuleExtensions
{
    public static IServiceCollection AddChargingModule(this IServiceCollection services, string connectionString)
    {
        _ = connectionString;
        return services;
    }
}
