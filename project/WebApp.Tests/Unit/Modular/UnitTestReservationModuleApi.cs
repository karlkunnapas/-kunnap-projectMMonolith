using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Charging;
using Shared.Contracts.Users;

namespace WebApp.Tests.Unit;

public class UnitTestReservationModuleApi : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public UnitTestReservationModuleApi(CustomWebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task UserReservationLifecycle_WorksViaChargingModuleApi()
    {
        using var scope = _factory.Services.CreateScope();
        var charging = scope.ServiceProvider.GetRequiredService<IChargingModuleApi>();
        var users = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();

        var userId = await users.GetUserIdByEmailAsync("karl@karl.com");
        Assert.NotNull(userId);

        var station = (await charging.GetStationsForHomeAsync(EStationStatus.Available)).First();
        var start = DateTime.UtcNow.AddMinutes(10);

        var reservation = await charging.CreateReservationAsync(new ReservationContract
        {
            Id = Guid.NewGuid(),
            UserId = userId!.Value,
            ChargingStationId = station.Id,
            StartTimeUtc = start,
            EndTimeUtc = start.AddMinutes(20),
            ExpiresAtUtc = start,
            EstimatedCost = 2m,
            Status = EReservationStatus.Active,
            StationName = station.Name
        });

        var userReservations = await charging.GetUserReservationsAsync(userId.Value);
        Assert.Contains(userReservations, r => r.Id == reservation.Id);
    }
}
