using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Companies.Application;
using Modules.Companies.Infrastructure;
using Shared.Contracts.Companies;

namespace Modules.Companies;

public static class CompaniesModuleExtensions
{
    public static IServiceCollection AddCompaniesModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CompaniesDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<ICompaniesModuleApi, CompaniesModuleApi>();
        return services;
    }
}
