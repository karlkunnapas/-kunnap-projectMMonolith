using App.BLL.DTOs;
using App.BLL.Services;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace WebApp.Tests.Unit;

public class UnitTestChargingSessionService
{
    [Fact]
    public async Task StartSessionAsync_ValidStartedReservation_CreatesActiveSession()
    {
        await using var context = BuildContext();
        var userId = Guid.NewGuid();
        var station = CreateStation();
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = station.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            Status = EReservationStatus.Started,
            EstimatedCost = 15
        };

        context.ChargingStations.Add(station);
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var reservationService = new Mock<IReservationService>();
        var sut = new ChargingSessionService(uow, reservationService.Object, new PricingService(uow));

        var result = await sut.StartSessionAsync(userId, new ChargingSessionStartRequestDto
        {
            StationId = station.Id,
            ReservationId = reservation.Id
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.IsActive);
        Assert.Equal(reservation.Id, result.Data.ReservationId);

        var persisted = await context.ChargingSessions.SingleAsync(s => s.ReservationId == reservation.Id);
        Assert.Equal(userId, persisted.UserId);
        Assert.Null(persisted.EndTime);
    }

    [Fact]
    public async Task StopSessionAsync_ActiveSession_SetsEndEnergyAndCost()
    {
        await using var context = BuildContext();
        var userId = Guid.NewGuid();
        var station = CreateStation();
        var session = new ChargingSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = station.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = null,
            EnergyConsumed = 0,
            Cost = 0
        };

        context.ChargingStations.Add(station);
        context.ChargingSessions.Add(session);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var reservationService = new Mock<IReservationService>();
        var sut = new ChargingSessionService(uow, reservationService.Object, new PricingService(uow));

        var result = await sut.StopSessionAsync(userId, session.Id, new ChargingSessionStopRequestDto());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data!.IsActive);
        Assert.True(result.Data.EnergyConsumedKwh > 0);
        Assert.True(result.Data.Cost > 0);

        var persisted = await context.ChargingSessions.SingleAsync(s => s.Id == session.Id);
        Assert.NotNull(persisted.EndTime);
        Assert.True(persisted.EnergyConsumed > 0);
        Assert.True(persisted.Cost > 0);
    }

    [Fact]
    public async Task StartSessionAsync_ForeignReservation_ReturnsForbidden()
    {
        await using var context = BuildContext();
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var station = CreateStation();
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = ownerUserId,
            ChargingStationId = station.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddMinutes(20),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            Status = EReservationStatus.Active,
            EstimatedCost = 12
        };

        context.ChargingStations.Add(station);
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var reservationService = new Mock<IReservationService>();
        var sut = new ChargingSessionService(uow, reservationService.Object, new PricingService(uow));

        var result = await sut.StartSessionAsync(otherUserId, new ChargingSessionStartRequestDto
        {
            StationId = station.Id,
            ReservationId = reservation.Id
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "FORBIDDEN");
    }

    private static ChargingStation CreateStation()
    {
        return new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr { ["en"] = "Session Station" },
            Location = "Tallinn",
            Status = EStationStatus.InUse,
            PricePerKwh = 0.40m,
            MaxPower = 150,
            IsActive = true,
            CompanyId = Guid.NewGuid()
        };
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
