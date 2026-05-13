using App.BLL.DTOs;
using App.BLL.Services;
using Moq;
using Shared.Contracts.Charging;
using Shared.Contracts.Users;

namespace WebApp.Tests.Unit;

public class UnitTestVehicleService
{
    [Fact]
    public async Task GetVehicleForUserAsync_ReturnsForbidden_WhenUsersModuleReturnsNull()
    {
        var usersApi = new Mock<IUsersModuleApi>();
        usersApi
            .Setup(x => x.GetVehicleForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVehicleContract?)null);
        var chargingApi = new Mock<IChargingModuleApi>();

        var service = new VehicleService(chargingApi.Object, usersApi.Object);
        var result = await service.GetVehicleForUserAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "FORBIDDEN");
    }

    [Fact]
    public async Task GetUserVehiclesAsync_MapsConnectorNamesFromChargingConnectors()
    {
        var connectorId = Guid.NewGuid();

        var usersApi = new Mock<IUsersModuleApi>();
        usersApi
            .Setup(x => x.GetUserVehiclesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserVehicleContract>
            {
                new()
                {
                    VehicleId = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    Make = "Tesla",
                    Model = "Model 3",
                    BatteryCapacity = 75,
                    ConnectorIds = new List<Guid> { connectorId }
                }
            });
        var chargingApi = new Mock<IChargingModuleApi>();
        chargingApi.Setup(x => x.GetConnectorsAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectorContract>
            {
                new() { Id = connectorId, Name = "CCS", IsActive = true }
            });

        var service = new VehicleService(chargingApi.Object, usersApi.Object);
        var result = await service.GetUserVehiclesAsync(Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Single(result.Data![0].CompatibleConnectors);
        Assert.Equal("CCS", result.Data[0].CompatibleConnectors[0].Name);
    }

    [Fact]
    public async Task SetConnectorCompatibilityAsync_ReturnsForbidden_WhenUsersModuleRejectsOwnership()
    {
        var usersApi = new Mock<IUsersModuleApi>();
        usersApi
            .Setup(x => x.SetConnectorCompatibilityAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var chargingApi = new Mock<IChargingModuleApi>();

        var service = new VehicleService(chargingApi.Object, usersApi.Object);
        var result = await service.SetConnectorCompatibilityAsync(Guid.NewGuid(), Guid.NewGuid(), new List<Guid> { Guid.NewGuid() });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "FORBIDDEN");
    }

    [Fact]
    public async Task GetCompatibleStationsForVehicleAsync_ReturnsOnlyStationsMatchingUsersConnectorIds()
    {
        var connectorMatchId = Guid.NewGuid();
        var connectorOtherId = Guid.NewGuid();
        var stationCompatibleId = Guid.NewGuid();

        var usersApi = new Mock<IUsersModuleApi>();
        usersApi
            .Setup(x => x.GetVehicleConnectorIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { connectorMatchId });
        var chargingApi = new Mock<IChargingModuleApi>();
        chargingApi.Setup(x => x.GetStationsForHomeAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChargingStationContract>
            {
                new()
                {
                    Id = stationCompatibleId,
                    Name = "Compatible station",
                    Location = "1km",
                    Status = Shared.Contracts.Charging.EStationStatus.Available,
                    IsActive = true,
                    Connectors = new List<ConnectorContract>
                    {
                        new() { Id = connectorMatchId, Name = "CCS", IsActive = true }
                    }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Incompatible station",
                    Location = "2km",
                    Status = Shared.Contracts.Charging.EStationStatus.Available,
                    IsActive = true,
                    Connectors = new List<ConnectorContract>
                    {
                        new() { Id = connectorOtherId, Name = "Type 2", IsActive = true }
                    }
                }
            });

        var service = new VehicleService(chargingApi.Object, usersApi.Object);
        var result = await service.GetCompatibleStationsForVehicleAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(stationCompatibleId, result.Data![0].StationId);
    }
}
