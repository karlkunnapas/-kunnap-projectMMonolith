using App.BLL.DTOs;
using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Tests.Unit;

public class UnitTestChargingStationService
{
    [Fact]
    public async Task GetHomePageAsync_FiltersByStatus()
    {
        await using var context = BuildContext();
        await SeedStationsAsync(context);

        await using var unitOfWork = new UnitOfWork(context);
        var service = new ChargingStationService(unitOfWork);

        var result = await service.GetHomePageAsync(new HomePageFilterDto { Status = EStationStatus.Available.ToString() });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Stations);
        Assert.Equal("Downtown", result.Data.Stations[0].Name);
    }

    [Fact]
    public async Task GetHomePageAsync_FiltersByConnector()
    {
        await using var context = BuildContext();
        await SeedStationsAsync(context);

        await using var unitOfWork = new UnitOfWork(context);
        var service = new ChargingStationService(unitOfWork);

        var result = await service.GetHomePageAsync(new HomePageFilterDto { Connector = "CCS" });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Stations);
        Assert.Equal("Downtown", result.Data.Stations[0].Name);
    }

    [Fact]
    public async Task GetHomePageAsync_FiltersByLocation()
    {
        await using var context = BuildContext();
        await SeedStationsAsync(context);

        await using var unitOfWork = new UnitOfWork(context);
        var service = new ChargingStationService(unitOfWork);

        var result = await service.GetHomePageAsync(new HomePageFilterDto { Location = "north" });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Stations);
        Assert.Equal("North", result.Data.Stations[0].Name);
    }

    [Fact]
    public async Task GetHomePageAsync_FiltersByStationName_WhenUsingLocationQuery()
    {
        await using var context = BuildContext();
        await SeedStationsAsync(context);

        await using var unitOfWork = new UnitOfWork(context);
        var service = new ChargingStationService(unitOfWork);

        var result = await service.GetHomePageAsync(new HomePageFilterDto { Location = "downtown" });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Stations);
        Assert.Equal("Downtown", result.Data.Stations[0].Name);
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedStationsAsync(AppDbContext context)
    {
        var ccs = new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "CCS", ["et"] = "CCS" },
            IsActive = true
        };

        var type2 = new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Type 2", ["et"] = "Type 2" },
            IsActive = true
        };

        var downtown = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Downtown", ["et"] = "Kesklinn" },
            Location = "Downtown area",
            Status = EStationStatus.Available,
            PricePerHour = 0.40m,
            MaxPower = 150m,
            IsActive = true
        };

        var north = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "North", ["et"] = "Pohja" },
            Location = "North side",
            Status = EStationStatus.Reserved,
            PricePerHour = 0.50m,
            MaxPower = 120m,
            IsActive = true
        };

        context.Connectors.AddRange(ccs, type2);
        context.ChargingStations.AddRange(downtown, north);
        context.ChargingStationConnectors.AddRange(
            new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = downtown.Id,
                ConnectorId = ccs.Id
            },
            new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = north.Id,
                ConnectorId = type2.Id
            });

        await context.SaveChangesAsync();
    }
}


