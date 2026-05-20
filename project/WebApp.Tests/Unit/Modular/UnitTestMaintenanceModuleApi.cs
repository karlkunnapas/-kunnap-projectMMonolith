using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;

namespace WebApp.Tests.Unit;

public class UnitTestMaintenanceModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestMaintenanceModuleApi(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task CreateAndUpdateMaintenance_WorksViaChargingModuleApi()
    {
        using var scope = _factory.Services.CreateScope();
        var companies = scope.ServiceProvider.GetRequiredService<ICompaniesModuleApi>();
        var charging = scope.ServiceProvider.GetRequiredService<IChargingModuleApi>();
        var users = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();

        var tenant = await companies.GetCompanyTenantBySlugAsync("seed-company");
        var reporterId = await users.GetUserIdByEmailAsync("karl@karl.com");
        Assert.NotNull(tenant);
        Assert.NotNull(reporterId);

        var station = (await charging.GetCompanyStationsAsync(tenant!.CompanyId)).First();

        var created = await charging.CreateMaintenanceAsync(new MaintenanceContract
        {
            Id = Guid.NewGuid(),
            CompanyId = tenant.CompanyId,
            ChargingStationId = station.Id,
            StationName = station.Name,
            ReportedByUserId = reporterId,
            IssueDescription = "Module test issue",
            Status = EMaintenanceStatus.Reported,
            ReportedAtUtc = DateTime.UtcNow
        });

        var updated = await charging.UpdateMaintenanceStatusAsync(created.Id, EMaintenanceStatus.InProgress, "Investigating", null);
        Assert.True(updated);
    }
}
