using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;

namespace WebApp.Tests.Unit;

public class UnitTestChargingStationCompanyModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestChargingStationCompanyModuleApi(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task CompanyStationQueries_WorkViaChargingModuleApi()
    {
        using var scope = _factory.Services.CreateScope();
        var companies = scope.ServiceProvider.GetRequiredService<ICompaniesModuleApi>();
        var charging = scope.ServiceProvider.GetRequiredService<IChargingModuleApi>();

        var tenant = await companies.GetCompanyTenantBySlugAsync("seed-company");
        Assert.NotNull(tenant);

        var stations = await charging.GetCompanyStationsAsync(tenant!.CompanyId);
        Assert.NotEmpty(stations);

        var station = await charging.GetCompanyStationByIdAsync(stations.First().Id, tenant.CompanyId);
        Assert.NotNull(station);
    }
}
