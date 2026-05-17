using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Charging;
using Shared.Contracts.Users;

namespace WebApp.Tests.Unit;

public class UnitTestVehicleModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestVehicleModuleApi(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task CreateUpdateDeleteVehicle_WorksViaUsersModuleApi()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();
        var charging = scope.ServiceProvider.GetRequiredService<IChargingModuleApi>();

        var userId = await users.GetUserIdByEmailAsync("karl@karl.com");
        Assert.NotNull(userId);

        var connectorId = (await charging.GetConnectorsAsync()).First().Id;
        var created = await users.CreateVehicleAsync(userId!.Value, new CreateUserVehicleContract
        {
            Make = "Tesla",
            Model = "Model 3",
            BatteryCapacity = 60,
            ConnectorIds = new[] { connectorId }
        });

        var updated = await users.UpdateVehicleAsync(created.VehicleId, userId.Value, new UpdateUserVehicleContract
        {
            Make = "Tesla",
            Model = "Model Y",
            BatteryCapacity = 75,
            ConnectorIds = new[] { connectorId }
        });

        Assert.NotNull(updated);
        Assert.Equal("Model Y", updated!.Model);

        var deleted = await users.DeleteVehicleAsync(created.VehicleId, userId.Value);
        Assert.True(deleted);
    }
}
