using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Charging;
using Shared.Contracts.Users;

namespace WebApp.Tests.Unit;

public class UnitTestChargingModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestChargingModuleApi(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetStationsForHome_ReturnsSeedStations()
    {
        using var scope = _factory.Services.CreateScope();
        var chargingApi = scope.ServiceProvider.GetRequiredService<IChargingModuleApi>();

        var stations = await chargingApi.GetStationsForHomeAsync();

        Assert.NotEmpty(stations);
        Assert.Contains(stations, s => s.Location.Contains("Kesklinna", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateAndCancelReservation_WorksThroughModuleApi()
    {
        using var scope = _factory.Services.CreateScope();
        var chargingApi = scope.ServiceProvider.GetRequiredService<IChargingModuleApi>();
        var usersApi = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();

        var userId = await usersApi.GetUserIdByEmailAsync("karl@karl.com");
        Assert.NotNull(userId);

        var station = (await chargingApi.GetStationsForHomeAsync(EStationStatus.Available)).First();
        var now = DateTime.UtcNow.AddMinutes(5);

        var created = await chargingApi.CreateReservationAsync(new ReservationContract
        {
            Id = Guid.NewGuid(),
            UserId = userId!.Value,
            ChargingStationId = station.Id,
            StartTimeUtc = now,
            EndTimeUtc = now.AddMinutes(30),
            ExpiresAtUtc = now,
            Status = EReservationStatus.Active,
            EstimatedCost = 0m
        });

        Assert.Equal(EReservationStatus.Active, created.Status);

        var cancelled = await chargingApi.UpdateReservationStatusAsync(
            created.Id,
            EReservationStatus.Cancelled,
            cancelledAtUtc: DateTime.UtcNow,
            stationStatus: EStationStatus.Available);

        Assert.True(cancelled);
    }
}
