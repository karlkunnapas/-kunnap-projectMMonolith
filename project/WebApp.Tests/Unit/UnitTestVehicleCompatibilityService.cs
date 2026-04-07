using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Tests.Unit;

public class UnitTestVehicleCompatibilityService
{
    [Fact]
    public async Task FilterStationsByVehicleAsync_ReturnsMatchingStationsOnly()
    {
        await using var context = BuildContext();

        var connectorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var station1 = new ChargingStation { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "A" }, Location = "A", IsActive = true, Status = EStationStatus.Available };
        var station2 = new ChargingStation { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "B" }, Location = "B", IsActive = true, Status = EStationStatus.Available };

        context.Vehicles.Add(new Vehicle { Id = vehicleId, UserId = userId, Make = "Test", Model = "Test" });
        context.VehicleConnectors.Add(new VehicleConnector { Id = Guid.NewGuid(), VehicleId = vehicleId, ConnectorId = connectorId });
        context.ChargingStations.AddRange(station1, station2);
        context.ChargingStationConnectors.AddRange(
            new ChargingStationConnector { Id = Guid.NewGuid(), ChargingStationId = station1.Id, ConnectorId = connectorId },
            new ChargingStationConnector { Id = Guid.NewGuid(), ChargingStationId = station2.Id, ConnectorId = Guid.NewGuid() });

        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new VehicleCompatibilityService(uow);

        var stations = await context.ChargingStations.Include(s => s.ChargingStationConnectors).ToListAsync();
        var result = await service.FilterStationsByVehicleAsync(stations, vehicleId);

        Assert.Single(result);
        Assert.Equal(station1.Id, result[0].Id);
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}

