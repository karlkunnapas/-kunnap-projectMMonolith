using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Companies;

namespace WebApp.Tests.Unit;

public class UnitTestAuditModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestAuditModuleApi(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task LogAuditMutation_PersistsAndCanBeQueried()
    {
        using var scope = _factory.Services.CreateScope();
        var companies = scope.ServiceProvider.GetRequiredService<ICompaniesModuleApi>();

        var tenant = await companies.GetCompanyTenantBySlugAsync("seed-company");
        Assert.NotNull(tenant);

        var entityId = Guid.NewGuid();
        await companies.LogAuditMutationAsync(tenant!.CompanyId, "tester", "Station", entityId, "Updated", "{}");

        var trail = await companies.GetAuditTrailAsync("Station", entityId, tenant.CompanyId);
        Assert.NotEmpty(trail.Entries);
    }
}
