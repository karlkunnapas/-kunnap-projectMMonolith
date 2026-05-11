using Microsoft.Extensions.DependencyInjection;

namespace Modules.Companies;

public static class CompaniesModuleExtensions
{
    public static IServiceCollection AddCompaniesModule(this IServiceCollection services, string connectionString)
    {
        _ = connectionString;
        return services;
    }
}
