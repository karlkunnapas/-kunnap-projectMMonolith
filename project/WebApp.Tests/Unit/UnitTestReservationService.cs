using App.BLL.DTOs;
using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace WebApp.Tests.Unit;

public class UnitTestReservationService
{
    [Fact]
    public async Task ReserveAsync_ValidRequest_CreatesActiveReservationWithExpiryFromStartTime()
    {
        await using var context = BuildContext();
        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Test Station" },
            Location = "Tallinn",
            Status = EStationStatus.Available,
            PricePerKwh = 12,
            MaxPower = 150,
            IsActive = true
        };

        context.ChargingStations.Add(station);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var promotionService = new Mock<App.BLL.Services.Interfaces.IPromotionService>();
        var service = new ReservationService(uow, new AvailabilityService(uow), new PricingService(uow), promotionService.Object);
        var userId = Guid.NewGuid();

        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddMinutes(90);

        var result = await service.ReserveAsync(userId, new ReservationCreateDto
        {
            StationId = station.Id,
            StartTimeUtc = start,
            EndTimeUtc = end,
            EstimatedEnergyKwh = 20
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(EReservationStatus.Active, result.Data!.Status);
        Assert.Equal(start.AddMinutes(15), result.Data.ExpiresAtUtc);
    }

    [Fact]
    public async Task ReserveAsync_WithPromotionCode_AppliesDiscountToEstimatedCost()
    {
        await using var context = BuildContext();
        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Discount Station" },
            Location = "Tallinn",
            CompanyId = Guid.NewGuid(),
            Status = EStationStatus.Available,
            PricePerKwh = 10m,
            MaxPower = 150,
            IsActive = true
        };
        var company = new Company
        {
            Id = station.CompanyId!.Value,
            Name = new LangStr("Discount Company"),
            ContactEmail = "discount-company@test.local",
            ContactPhone = "+3725000000",
            Slug = "discount-company",
            IsActive = true
        };

        context.Companies.Add(company);
        context.ChargingStations.Add(station);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var promotionService = new Mock<App.BLL.Services.Interfaces.IPromotionService>();
        promotionService
            .Setup(s => s.ValidateUserPromotionForCompanyAsync(It.IsAny<Guid>(), station.CompanyId!.Value, "SAVE20"))
            .ReturnsAsync(ServiceResult<App.BLL.DTOs.AppliedPromotionDto>.Ok(new App.BLL.DTOs.AppliedPromotionDto
            {
                PromotionId = Guid.NewGuid(),
                Code = "SAVE20",
                DiscountValue = 20m
            }));

        var service = new ReservationService(uow, new AvailabilityService(uow), new PricingService(uow), promotionService.Object);
        var userId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddHours(1);

        var result = await service.ReserveAsync(userId, new ReservationCreateDto
        {
            StationId = station.Id,
            StartTimeUtc = start,
            EndTimeUtc = end,
            EstimatedEnergyKwh = 10m,
            PromotionCode = "SAVE20"
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(80m, result.Data!.EstimatedCost);

        var persisted = await context.Reservations.SingleAsync(r => r.UserId == userId);
        Assert.NotNull(persisted.PromotionId);
    }

    [Fact]
    public async Task ReserveAsync_OverlappingReservation_ReturnsOverlapError()
    {
        await using var context = BuildContext();
        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Overlap Station" },
            Location = "Tartu",
            Status = EStationStatus.Available,
            PricePerKwh = 10,
            MaxPower = 75,
            IsActive = true
        };

        context.ChargingStations.Add(station);
        context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ChargingStationId = station.Id,
            StartTime = DateTime.UtcNow.AddHours(2),
            EndTime = DateTime.UtcNow.AddHours(3),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
            Status = EReservationStatus.Active,
            EstimatedCost = 10
        });

        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var promotionService = new Mock<App.BLL.Services.Interfaces.IPromotionService>();
        var service = new ReservationService(uow, new AvailabilityService(uow), new PricingService(uow), promotionService.Object);

        var result = await service.ReserveAsync(Guid.NewGuid(), new ReservationCreateDto
        {
            StationId = station.Id,
            StartTimeUtc = DateTime.UtcNow.AddHours(2).AddMinutes(15),
            EndTimeUtc = DateTime.UtcNow.AddHours(2).AddMinutes(45)
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "OVERLAP");
    }

    [Fact]
    public async Task ReserveAsync_StationUnavailable_ReturnsStationUnavailableError()
    {
        await using var context = BuildContext();
        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Unavailable Station" },
            Location = "Parnu",
            Status = EStationStatus.Maintenance,
            PricePerKwh = 11,
            MaxPower = 50,
            IsActive = true
        };

        context.ChargingStations.Add(station);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var promotionService = new Mock<App.BLL.Services.Interfaces.IPromotionService>();
        var service = new ReservationService(uow, new AvailabilityService(uow), new PricingService(uow), promotionService.Object);

        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddMinutes(45);

        var result = await service.ReserveAsync(Guid.NewGuid(), new ReservationCreateDto
        {
            StationId = station.Id,
            StartTimeUtc = start,
            EndTimeUtc = end,
            EstimatedEnergyKwh = 12
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "STATION_UNAVAILABLE");
    }

    [Fact]
    public async Task StartReservationAsync_ValidReservation_SetsStartedStatusAndStationInUse()
    {
        await using var context = BuildContext();
        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Start Station" },
            Location = "Tallinn",
            Status = EStationStatus.Available,
            PricePerKwh = 12,
            MaxPower = 100,
            IsActive = true
        };
        var userId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = station.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-2),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(13),
            Status = EReservationStatus.Active,
            EstimatedCost = 5
        };

        context.ChargingStations.Add(station);
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var promotionService = new Mock<App.BLL.Services.Interfaces.IPromotionService>();
        var service = new ReservationService(uow, new AvailabilityService(uow), new PricingService(uow), promotionService.Object);

        var result = await service.StartReservationAsync(reservation.Id, userId);
        Assert.True(result.Success);

        var updatedReservation = await context.Reservations.FirstAsync(r => r.Id == reservation.Id);
        var updatedStation = await context.ChargingStations.FirstAsync(s => s.Id == station.Id);

        Assert.Equal(EReservationStatus.Started, updatedReservation.Status);
        Assert.Equal(EStationStatus.InUse, updatedStation.Status);
    }

    [Fact]
    public async Task CancelReservationAsync_StartedReservation_ReturnsValidationError()
    {
        await using var context = BuildContext();
        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Started Station" },
            Location = "Tartu",
            Status = EStationStatus.InUse,
            PricePerKwh = 11,
            MaxPower = 50,
            IsActive = true
        };
        var userId = Guid.NewGuid();
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = station.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(20),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(20),
            Status = EReservationStatus.Started,
            EstimatedCost = 8
        };

        context.ChargingStations.Add(station);
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var promotionService = new Mock<App.BLL.Services.Interfaces.IPromotionService>();
        var service = new ReservationService(uow, new AvailabilityService(uow), new PricingService(uow), promotionService.Object);

        var result = await service.CancelReservationAsync(reservation.Id, userId);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "VALIDATION");
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
