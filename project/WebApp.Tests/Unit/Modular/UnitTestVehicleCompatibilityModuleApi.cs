using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Charging;
using Shared.Contracts.Users;

namespace WebApp.Tests.Unit;

public class UnitTestVehicleCompatibilityModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestVehicleCompatibilityModuleApi(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task SetConnectorCompatibility_PersistsConnectorIds()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();
        var charging = scope.ServiceProvider.GetRequiredService<IChargingModuleApi>();

        var userId = await users.GetUserIdByEmailAsync("karl@karl.com");
        Assert.NotNull(userId);

        var connectors = await charging.GetConnectorsAsync();
        var connectorIds = connectors.Take(2).Select(c => c.Id).ToArray();

        var vehicle = await users.CreateVehicleAsync(userId!.Value, new CreateUserVehicleContract
        {
            Make = "VW",
            Model = "ID.4",
            ConnectorIds = connectorIds
        });

        var ok = await users.SetConnectorCompatibilityAsync(vehicle.VehicleId, userId.Value, connectorIds);
        Assert.True(ok);

        var persisted = await users.GetVehicleConnectorIdsAsync(vehicle.VehicleId, userId.Value);
        Assert.Equal(connectorIds.OrderBy(x => x), persisted.OrderBy(x => x));
    }
}
