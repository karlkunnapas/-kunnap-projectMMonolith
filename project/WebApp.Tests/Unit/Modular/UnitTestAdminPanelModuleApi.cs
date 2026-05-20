using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;

namespace WebApp.Tests.Unit;

public class UnitTestAdminPanelModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestAdminPanelModuleApi(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task AdminReadModels_AreAvailableViaModuleApis()
    {
        using var scope = _factory.Services.CreateScope();
        var companies = scope.ServiceProvider.GetRequiredService<ICompaniesModuleApi>();
        var charging = scope.ServiceProvider.GetRequiredService<IChargingModuleApi>();

        var adminCompanies = await companies.GetCompaniesForAdminAsync();
        var adminStations = await charging.GetStationsForAdminAsync();

        Assert.NotEmpty(adminCompanies);
        Assert.NotEmpty(adminStations);
    }
}
