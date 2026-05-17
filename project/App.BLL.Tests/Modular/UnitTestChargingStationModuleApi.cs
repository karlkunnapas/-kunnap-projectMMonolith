using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Charging;

namespace WebApp.Tests.Unit;

public class UnitTestChargingStationModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestChargingStationModuleApi(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task StationDetailsAndConnectors_AreReturned()
    {
        using var scope = _factory.Services.CreateScope();
        var charging = scope.ServiceProvider.GetRequiredService<IChargingModuleApi>();

        var station = (await charging.GetStationsForHomeAsync()).First();
        var details = await charging.GetStationByIdAsync(station.Id);
        var connectorIds = await charging.GetStationConnectorIdsAsync(station.Id);

        Assert.NotNull(details);
        Assert.NotEmpty(connectorIds);
    }
}
