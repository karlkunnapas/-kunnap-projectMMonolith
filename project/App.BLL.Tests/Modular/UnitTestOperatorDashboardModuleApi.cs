using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;

namespace WebApp.Tests.Unit;

public class UnitTestOperatorDashboardModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestOperatorDashboardModuleApi(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task CompanyDashboardStats_ReturnsStationCounts()
    {
        using var scope = _factory.Services.CreateScope();
        var companies = scope.ServiceProvider.GetRequiredService<ICompaniesModuleApi>();
        var charging = scope.ServiceProvider.GetRequiredService<IChargingModuleApi>();

        var tenant = await companies.GetCompanyTenantBySlugAsync("seed-company");
        Assert.NotNull(tenant);

        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow.AddDays(1);
        var stats = await charging.GetCompanyDashboardStatsAsync(tenant!.CompanyId, from, to);

        Assert.True(stats.TotalStations >= 1);
    }
}
