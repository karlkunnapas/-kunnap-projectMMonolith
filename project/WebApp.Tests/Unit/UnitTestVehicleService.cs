using App.BLL.DTOs;
using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shared.Contracts.Users;

namespace WebApp.Tests.Unit;

public class UnitTestVehicleService
{
    [Fact]
    public async Task GetVehicleForUserAsync_ReturnsForbidden_WhenUsersModuleReturnsNull()
    {
        await using var context = BuildContext();
        await using var uow = new UnitOfWork(context);

        var usersApi = new Mock<IUsersModuleApi>();
        usersApi
            .Setup(x => x.GetVehicleForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVehicleContract?)null);

        var service = new VehicleService(uow, usersApi.Object);
        var result = await service.GetVehicleForUserAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "FORBIDDEN");
    }

    [Fact]
    public async Task GetUserVehiclesAsync_MapsConnectorNamesFromChargingConnectors()
    {
        await using var context = BuildContext();

        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "CCS" },
            IsActive = true
        };
        context.Connectors.Add(connector);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);

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
                    ConnectorIds = new List<Guid> { connector.Id }
                }
            });

        var service = new VehicleService(uow, usersApi.Object);
        var result = await service.GetUserVehiclesAsync(Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Single(result.Data![0].CompatibleConnectors);
        Assert.Equal("CCS", result.Data[0].CompatibleConnectors[0].Name);
    }

    [Fact]
    public async Task SetConnectorCompatibilityAsync_ReturnsForbidden_WhenUsersModuleRejectsOwnership()
    {
        await using var context = BuildContext();
        await using var uow = new UnitOfWork(context);

        var usersApi = new Mock<IUsersModuleApi>();
        usersApi
            .Setup(x => x.SetConnectorCompatibilityAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new VehicleService(uow, usersApi.Object);
        var result = await service.SetConnectorCompatibilityAsync(Guid.NewGuid(), Guid.NewGuid(), new List<Guid> { Guid.NewGuid() });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "FORBIDDEN");
    }

    [Fact]
    public async Task GetCompatibleStationsForVehicleAsync_ReturnsOnlyStationsMatchingUsersConnectorIds()
    {
        await using var context = BuildContext();

        var connectorMatch = new Connector { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "CCS" }, IsActive = true };
        var connectorOther = new Connector { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "Type 2" }, IsActive = true };
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("Vehicle Test Company"),
            ContactEmail = "vehicle-company@test.local",
            ContactPhone = "+3725999999",
            Slug = "vehicle-test-company",
            IsActive = true
        };

        var stationCompatible = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Compatible station" },
            Location = "1km",
            Status = EStationStatus.Available,
            IsActive = true,
            CompanyId = company.Id
        };

        var stationIncompatible = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Incompatible station" },
            Location = "2km",
            Status = EStationStatus.Available,
            IsActive = true,
            CompanyId = company.Id
        };

        context.Companies.Add(company);
        context.Connectors.AddRange(connectorMatch, connectorOther);
        context.ChargingStations.AddRange(stationCompatible, stationIncompatible);
        context.ChargingStationConnectors.AddRange(
            new ChargingStationConnector { Id = Guid.NewGuid(), ChargingStationId = stationCompatible.Id, ConnectorId = connectorMatch.Id },
            new ChargingStationConnector { Id = Guid.NewGuid(), ChargingStationId = stationIncompatible.Id, ConnectorId = connectorOther.Id });

        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);

        var usersApi = new Mock<IUsersModuleApi>();
        usersApi
            .Setup(x => x.GetVehicleConnectorIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { connectorMatch.Id });

        var service = new VehicleService(uow, usersApi.Object);
        var result = await service.GetCompatibleStationsForVehicleAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(stationCompatible.Id, result.Data![0].StationId);
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
