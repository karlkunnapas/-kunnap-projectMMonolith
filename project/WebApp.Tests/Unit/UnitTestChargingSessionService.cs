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
        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            Code = "RESERVE10",
            DiscountValue = 10m,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(5),
            IsActive = true
        };
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = station.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow.AddMinutes(30),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            Status = EReservationStatus.Started,
            EstimatedCost = 15,
            PromotionId = promotion.Id
        };

        context.Promotions.Add(promotion);
        context.ChargingStations.Add(station);
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var reservationService = new Mock<IReservationService>();
        var promotionService = new Mock<IPromotionService>();
        var sut = new ChargingSessionService(uow, reservationService.Object, new PricingService(uow), promotionService.Object);

        var result = await sut.StartSessionAsync(userId, new ChargingSessionStartRequestDto
        {
            StationId = station.Id,
            ReservationId = reservation.Id
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.IsActive);
        Assert.Equal(reservation.Id, result.Data.ReservationId);
        Assert.Equal(promotion.Code, result.Data.PromotionCode);

        var persisted = await context.ChargingSessions.SingleAsync(s => s.ReservationId == reservation.Id);
        Assert.Equal(userId, persisted.UserId);
        Assert.Null(persisted.EndTime);
        Assert.Equal(reservation.PromotionId, persisted.PromotionId);
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
        var promotionService = new Mock<IPromotionService>();
        var sut = new ChargingSessionService(uow, reservationService.Object, new PricingService(uow), promotionService.Object);

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
    public async Task StopSessionAsync_WithPromotionCode_AppliesDiscount()
    {
        await using var context = BuildContext();
        var userId = Guid.NewGuid();
        var station = CreateStation();
        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            Code = "SAVE25",
            DiscountValue = 25m,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(1),
            IsActive = true,
            CompanyId = station.CompanyId
        };
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

        context.Promotions.Add(promotion);
        context.UserPromotions.Add(new UserPromotion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PromotionId = promotion.Id,
            AddedAt = DateTime.UtcNow
        });
        context.ChargingStations.Add(station);
        context.ChargingSessions.Add(session);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var reservationService = new Mock<IReservationService>();
        var promotionService = new Mock<IPromotionService>();
        promotionService
            .Setup(s => s.ValidateUserPromotionForCompanyAsync(userId, station.CompanyId!.Value, "SAVE25"))
            .ReturnsAsync(ServiceResult<AppliedPromotionDto>.Ok(new AppliedPromotionDto
            {
                PromotionId = promotion.Id,
                Code = "SAVE25",
                DiscountValue = 25m
            }));

        var sut = new ChargingSessionService(uow, reservationService.Object, new PricingService(uow), promotionService.Object);

        var result = await sut.StopSessionAsync(userId, session.Id, new ChargingSessionStopRequestDto
        {
            PromotionCode = "SAVE25"
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.Cost > 0);
        Assert.True(result.Data.Cost < 60m);

        var persisted = await context.ChargingSessions.SingleAsync(s => s.Id == session.Id);
        Assert.NotNull(persisted.PromotionId);
        var consumed = await context.UserPromotions.SingleAsync(up => up.UserId == userId && up.PromotionId == promotion.Id);
        Assert.True(consumed.IsUsed);
    }

    [Fact]
    public async Task StopSessionAsync_WithReservationPromotion_UsesLockedPromotionAndConsumesIt()
    {
        await using var context = BuildContext();
        var userId = Guid.NewGuid();
        var station = CreateStation();
        var lockedPromotion = new Promotion
        {
            Id = Guid.NewGuid(),
            Code = "LOCKED15",
            DiscountValue = 15m,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(2),
            IsActive = true,
            CompanyId = station.CompanyId
        };
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = station.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-40),
            EndTime = DateTime.UtcNow.AddMinutes(20),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(15),
            EstimatedCost = 20m,
            Status = EReservationStatus.Started,
            PromotionId = lockedPromotion.Id
        };
        var session = new ChargingSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = station.Id,
            ReservationId = reservation.Id,
            PromotionId = lockedPromotion.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = null
        };

        context.Promotions.Add(lockedPromotion);
        context.UserPromotions.Add(new UserPromotion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PromotionId = lockedPromotion.Id,
            AddedAt = DateTime.UtcNow
        });
        context.ChargingStations.Add(station);
        context.Reservations.Add(reservation);
        context.ChargingSessions.Add(session);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var reservationService = new Mock<IReservationService>();
        var promotionService = new Mock<IPromotionService>(MockBehavior.Strict);
        var sut = new ChargingSessionService(uow, reservationService.Object, new PricingService(uow), promotionService.Object);

        var result = await sut.StopSessionAsync(userId, session.Id, new ChargingSessionStopRequestDto
        {
            PromotionCode = "IGNOREDCODE"
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("LOCKED15", result.Data!.PromotionCode);
        Assert.True(result.Data.DiscountPercent > 0m);
        var lockedConsumed = await context.UserPromotions.SingleAsync(up => up.UserId == userId && up.PromotionId == lockedPromotion.Id);
        Assert.True(lockedConsumed.IsUsed);
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
        var promotionService = new Mock<IPromotionService>();
        var sut = new ChargingSessionService(uow, reservationService.Object, new PricingService(uow), promotionService.Object);

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
