using App.BLL.DTOs;
using App.BLL.Services;
using Shared.Contracts.Charging;
using Shared.Contracts.Users;
using Moq;

namespace WebApp.Tests.Unit;

public class UnitTestChargingStationService
{
    [Fact]
    public async Task GetHomePageAsync_FiltersByStatus()
    {
        var chargingApi = BuildChargingApiMock();
        var usersApi = new Mock<IUsersModuleApi>();
        var service = new ChargingStationService(chargingApi.Object, usersApi.Object);

        var result = await service.GetHomePageAsync(new HomePageFilterDto { Status = EStationStatus.Available.ToString() });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Stations);
        Assert.Equal("Downtown", result.Data.Stations[0].Name);
    }

    [Fact]
    public async Task GetHomePageAsync_FiltersByConnector()
    {
        var chargingApi = BuildChargingApiMock();
        var usersApi = new Mock<IUsersModuleApi>();
        var service = new ChargingStationService(chargingApi.Object, usersApi.Object);

        var result = await service.GetHomePageAsync(new HomePageFilterDto { Connector = "CCS" });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Stations);
        Assert.Equal("Downtown", result.Data.Stations[0].Name);
    }

    [Fact]
    public async Task GetHomePageAsync_FiltersByLocation()
    {
        var chargingApi = BuildChargingApiMock();
        var usersApi = new Mock<IUsersModuleApi>();
        var service = new ChargingStationService(chargingApi.Object, usersApi.Object);

        var result = await service.GetHomePageAsync(new HomePageFilterDto { Location = "north" });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Stations);
        Assert.Equal("North", result.Data.Stations[0].Name);
    }

    [Fact]
    public async Task GetHomePageAsync_FiltersByVehicleCompatibility()
    {
        var chargingApi = BuildChargingApiMock();
        var usersApi = new Mock<IUsersModuleApi>();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        usersApi
            .Setup(x => x.GetVehicleForUserAsync(vehicleId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVehicleContract
            {
                VehicleId = vehicleId,
                UserId = userId,
                Make = "Tesla",
                Model = "Model 3",
                ConnectorIds = new[] { Guid.Parse("11111111-1111-1111-1111-111111111111") }
            });

        var service = new ChargingStationService(chargingApi.Object, usersApi.Object);
        var result = await service.GetHomePageAsync(new HomePageFilterDto { VehicleId = vehicleId, UserId = userId });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Stations);
        Assert.Equal("Downtown", result.Data.Stations[0].Name);
    }

    private static Mock<IChargingModuleApi> BuildChargingApiMock()
    {
        var ccsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var type2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var allStations = new List<ChargingStationContract>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Downtown",
                Location = "Downtown area",
                Status = EStationStatus.Available,
                PricePerKwh = 0.40m,
                MaxPower = 150m,
                IsActive = true,
                Connectors = new List<ConnectorContract>
                {
                    new() { Id = ccsId, Name = "CCS", IsActive = true }
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "North",
                Location = "North side",
                Status = EStationStatus.InUse,
                PricePerKwh = 0.50m,
                MaxPower = 120m,
                IsActive = true,
                Connectors = new List<ConnectorContract>
                {
                    new() { Id = type2Id, Name = "Type 2", IsActive = true }
                }
            }
        };

        var mock = new Mock<IChargingModuleApi>();
        mock.Setup(x => x.GetStationsForHomeAsync(It.IsAny<EStationStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EStationStatus? status, CancellationToken _) =>
                status.HasValue ? allStations.Where(s => s.Status == status.Value).ToList() : allStations);

        return mock;
    }
}
