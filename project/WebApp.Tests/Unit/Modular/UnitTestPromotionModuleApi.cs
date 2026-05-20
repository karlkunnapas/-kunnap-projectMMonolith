using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Companies;

namespace WebApp.Tests.Unit;

public class UnitTestPromotionModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestPromotionModuleApi(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task CompanyPromotions_QueryWorksForSeedCompany()
    {
        using var scope = _factory.Services.CreateScope();
        var companies = scope.ServiceProvider.GetRequiredService<ICompaniesModuleApi>();

        var tenant = await companies.GetCompanyTenantBySlugAsync("seed-company");
        Assert.NotNull(tenant);

        var promotions = await companies.GetCompanyPromotionsAsync(tenant!.CompanyId);
        Assert.NotNull(promotions);
    }
}
