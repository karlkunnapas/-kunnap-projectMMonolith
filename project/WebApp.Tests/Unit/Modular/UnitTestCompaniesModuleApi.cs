using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;

namespace WebApp.Tests.Unit;

public class UnitTestCompaniesModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestCompaniesModuleApi(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCompanyTenantBySlug_ReturnsSeedCompany()
    {
        using var scope = _factory.Services.CreateScope();
        var companiesApi = scope.ServiceProvider.GetRequiredService<ICompaniesModuleApi>();

        var tenant = await companiesApi.GetCompanyTenantBySlugAsync("seed-company");

        Assert.NotNull(tenant);
        Assert.True(tenant!.CompanyId != Guid.Empty);
        Assert.True(tenant.IsActive);
    }

    [Fact]
    public async Task OwnerUser_HasOwnerRoleInSeedCompany()
    {
        using var scope = _factory.Services.CreateScope();
        var usersApi = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();
        var companiesApi = scope.ServiceProvider.GetRequiredService<ICompaniesModuleApi>();

        var ownerId = await usersApi.GetUserIdByEmailAsync("owner@seed.com");
        Assert.NotNull(ownerId);

        var tenant = await companiesApi.GetCompanyTenantBySlugAsync("seed-company");
        Assert.NotNull(tenant);

        var hasOwnerRole = await companiesApi.HasCompanyRoleAsync(tenant!.CompanyId, ownerId!.Value, "Owner");
        Assert.True(hasOwnerRole);
    }
}
