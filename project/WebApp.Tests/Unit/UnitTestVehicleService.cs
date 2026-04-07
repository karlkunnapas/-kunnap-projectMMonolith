using App.BLL.DTOs;
using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Tests.Unit;

public class UnitTestVehicleService
{
    [Fact]
    public async Task CreateVehicleAsync_CreatesVehicleWithCompatibility()
    {
        await using var context = BuildContext();
        var connector = new Connector { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "CCS", ["et"] = "CCS" }, IsActive = true };
        context.Connectors.Add(connector);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new VehicleService(uow);
        var userId = Guid.NewGuid();

        var result = await service.CreateVehicleAsync(userId, new VehicleCreateDto
        {
            Make = "Tesla",
            Model = "Model 3",
            BatteryCapacity = 75,
            ConnectorIds = new List<Guid> { connector.Id }
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.CompatibleConnectors);
        Assert.Equal("CCS", result.Data.CompatibleConnectors[0].Name);
    }

    [Fact]
    public async Task GetVehicleForUserAsync_ReturnsForbiddenForForeignUser()
    {
        await using var context = BuildContext();
        var ownerId = Guid.NewGuid();
        var foreignUserId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            Make = "VW",
            Model = "ID.4"
        };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new VehicleService(uow);

        var result = await service.GetVehicleForUserAsync(vehicle.Id, foreignUserId);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "FORBIDDEN");
    }

    [Fact]
    public async Task GetCompatibleStationsForVehicleAsync_ReturnsOnlyCompatibleStations()
    {
        await using var context = BuildContext();

        var ccs = new Connector { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "CCS" }, IsActive = true };
        var type2 = new Connector { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "Type 2" }, IsActive = true };

        var stationCompatible = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Compatible station" },
            Location = "1km",
            Status = EStationStatus.Available,
            IsActive = true
        };

        var stationIncompatible = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Incompatible station" },
            Location = "2km",
            Status = EStationStatus.Available,
            IsActive = true
        };

        var userId = Guid.NewGuid();
        var vehicle = new Vehicle { Id = Guid.NewGuid(), UserId = userId, Make = "Kia", Model = "EV6" };

        context.Connectors.AddRange(ccs, type2);
        context.ChargingStations.AddRange(stationCompatible, stationIncompatible);
        context.Vehicles.Add(vehicle);
        context.VehicleConnectors.Add(new VehicleConnector { Id = Guid.NewGuid(), VehicleId = vehicle.Id, ConnectorId = ccs.Id });
        context.ChargingStationConnectors.AddRange(
            new ChargingStationConnector { Id = Guid.NewGuid(), ChargingStationId = stationCompatible.Id, ConnectorId = ccs.Id },
            new ChargingStationConnector { Id = Guid.NewGuid(), ChargingStationId = stationIncompatible.Id, ConnectorId = type2.Id });

        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new VehicleService(uow);

        var result = await service.GetCompatibleStationsForVehicleAsync(vehicle.Id, userId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!);
        Assert.Equal(stationCompatible.Id, result.Data[0].StationId);
    }

    [Fact]
    public async Task GetUserVehiclesAsync_ReturnsConnectorCount_FromCompatibilityMappings()
    {
        await using var context = BuildContext();

        var userId = Guid.NewGuid();
        var connector1 = new Connector { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "CCS" }, IsActive = true };
        var connector2 = new Connector { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "Type 2" }, IsActive = true };

        context.Connectors.AddRange(connector1, connector2);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new VehicleService(uow);

        var createResult = await service.CreateVehicleAsync(userId, new VehicleCreateDto
        {
            Make = "Hyundai",
            Model = "Ioniq 5",
            ConnectorIds = new List<Guid> { connector1.Id, connector2.Id }
        });

        Assert.True(createResult.Success);

        var listResult = await service.GetUserVehiclesAsync(userId);

        Assert.True(listResult.Success);
        Assert.Single(listResult.Data!);
        Assert.Equal(2, listResult.Data![0].CompatibleConnectors.Count);
    }

    [Fact]
    public async Task UpdateVehicleAsync_ReplacesCompatibility_WithoutTrackingConflict()
    {
        await using var context = BuildContext();

        var userId = Guid.NewGuid();
        var connector1 = new Connector { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "CCS" }, IsActive = true };
        var connector2 = new Connector { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "Type 2" }, IsActive = true };

        context.Connectors.AddRange(connector1, connector2);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new VehicleService(uow);

        var createResult = await service.CreateVehicleAsync(userId, new VehicleCreateDto
        {
            Make = "BMW",
            Model = "i4",
            ConnectorIds = new List<Guid> { connector1.Id }
        });

        Assert.True(createResult.Success);
        var vehicleId = createResult.Data!.Id;

        var updateResult = await service.UpdateVehicleAsync(vehicleId, userId, new VehicleUpdateDto
        {
            Make = "BMW",
            Model = "i4 M50",
            ConnectorIds = new List<Guid> { connector2.Id }
        });

        Assert.True(updateResult.Success);
        Assert.NotNull(updateResult.Data);
        Assert.Single(updateResult.Data!.CompatibleConnectors);
        Assert.Equal(connector2.Id, updateResult.Data.CompatibleConnectors[0].ConnectorId);
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
