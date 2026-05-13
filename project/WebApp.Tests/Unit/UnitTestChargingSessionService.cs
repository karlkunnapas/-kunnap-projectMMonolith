using App.BLL.DTOs;
using App.BLL.Services;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Moq;
using SC = Shared.Contracts.Charging;

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

        var chargingApi = CreateChargingModuleApiForContext(context);
        var reservationService = new Mock<IReservationService>();
        var promotionService = new Mock<IPromotionService>();
        promotionService.Setup(s => s.GetUserPromotionsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(ServiceResult<List<UserPromotionDto>>.Ok(new List<UserPromotionDto>()));
        var sut = new ChargingSessionService(chargingApi.Object, reservationService.Object, new PricingService(chargingApi.Object), promotionService.Object);

        var result = await sut.StartSessionAsync(userId, new ChargingSessionStartRequestDto
        {
            StationId = station.Id,
            ReservationId = reservation.Id
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.IsActive);
        Assert.Equal(reservation.Id, result.Data.ReservationId);
        Assert.NotNull(result.Data.PromotionCode);

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

        var chargingApi = CreateChargingModuleApiForContext(context);
        var reservationService = new Mock<IReservationService>();
        var promotionService = new Mock<IPromotionService>();
        promotionService.Setup(s => s.GetUserPromotionsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(ServiceResult<List<UserPromotionDto>>.Ok(new List<UserPromotionDto>()));
        var sut = new ChargingSessionService(chargingApi.Object, reservationService.Object, new PricingService(chargingApi.Object), promotionService.Object);

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
        var companyId = Guid.NewGuid();
        station.CompanyId = companyId;
        var company = new Company
        {
            Id = companyId,
            Name = new LangStr("Session Promotion Company"),
            ContactEmail = "session-promo@test.local",
            ContactPhone = "+3726111111",
            Slug = "session-promo-company",
            IsActive = true
        };
        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            Code = "SAVE25",
            DiscountValue = 25m,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(1),
            IsActive = true,
            CompanyId = companyId
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

        context.Companies.Add(company);
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

        var chargingApi = CreateChargingModuleApiForContext(context);
        var reservationService = new Mock<IReservationService>();
        var promotionService = new Mock<IPromotionService>();
        promotionService
            .Setup(s => s.ValidateUserPromotionForCompanyAsync(userId, companyId, "SAVE25"))
            .ReturnsAsync(ServiceResult<AppliedPromotionDto>.Ok(new AppliedPromotionDto
            {
                PromotionId = promotion.Id,
                Code = "SAVE25",
                DiscountValue = 25m
            }));

        promotionService.Setup(s => s.GetUserPromotionsAsync(userId))
            .ReturnsAsync(ServiceResult<List<UserPromotionDto>>.Ok(new List<UserPromotionDto>
            {
                new()
                {
                    Id = context.UserPromotions.Single(up => up.UserId == userId && up.PromotionId == promotion.Id).Id,
                    PromotionId = promotion.Id,
                    Code = promotion.Code,
                    DiscountValue = promotion.DiscountValue,
                    AddedAtUtc = DateTime.UtcNow,
                    IsActive = true,
                    IsUsed = false,
                    ValidFromUtc = promotion.ValidFrom,
                    ValidToUtc = promotion.ValidTo
                }
            }));
        promotionService.Setup(s => s.RemoveUserPromotionAsync(userId, It.IsAny<Guid>()))
            .ReturnsAsync(ServiceResult.Ok());

        var sut = new ChargingSessionService(chargingApi.Object, reservationService.Object, new PricingService(chargingApi.Object), promotionService.Object);

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
        promotionService.Verify(s => s.RemoveUserPromotionAsync(userId, It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task StopSessionAsync_WithReservationPromotion_UsesLockedPromotionAndConsumesIt()
    {
        await using var context = BuildContext();
        var userId = Guid.NewGuid();
        var station = CreateStation();
        var companyId = Guid.NewGuid();
        station.CompanyId = companyId;
        var company = new Company
        {
            Id = companyId,
            Name = new LangStr("Locked Promotion Company"),
            ContactEmail = "locked-promo@test.local",
            ContactPhone = "+3726222222",
            Slug = "locked-promo-company",
            IsActive = true
        };
        var lockedPromotion = new Promotion
        {
            Id = Guid.NewGuid(),
            Code = "LOCKED15",
            DiscountValue = 15m,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(2),
            IsActive = true,
            CompanyId = companyId
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

        context.Companies.Add(company);
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

        var chargingApi = CreateChargingModuleApiForContext(context);
        var reservationService = new Mock<IReservationService>();
        var promotionService = new Mock<IPromotionService>(MockBehavior.Strict);
        promotionService.Setup(s => s.GetUserPromotionsAsync(userId))
            .ReturnsAsync(ServiceResult<List<UserPromotionDto>>.Ok(new List<UserPromotionDto>
            {
                new()
                {
                    Id = context.UserPromotions.Single(up => up.UserId == userId && up.PromotionId == lockedPromotion.Id).Id,
                    PromotionId = lockedPromotion.Id,
                    Code = lockedPromotion.Code,
                    DiscountValue = lockedPromotion.DiscountValue,
                    AddedAtUtc = DateTime.UtcNow,
                    IsActive = true,
                    IsUsed = false,
                    ValidFromUtc = lockedPromotion.ValidFrom,
                    ValidToUtc = lockedPromotion.ValidTo
                }
            }));
        promotionService.Setup(s => s.RemoveUserPromotionAsync(userId, It.IsAny<Guid>()))
            .ReturnsAsync(ServiceResult.Ok());
        var sut = new ChargingSessionService(chargingApi.Object, reservationService.Object, new PricingService(chargingApi.Object), promotionService.Object);

        var result = await sut.StopSessionAsync(userId, session.Id, new ChargingSessionStopRequestDto
        {
            PromotionCode = "IGNOREDCODE"
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("LOCKED15", result.Data!.PromotionCode);
        Assert.True(result.Data.DiscountPercent > 0m);
        promotionService.Verify(s => s.RemoveUserPromotionAsync(userId, It.IsAny<Guid>()), Times.Once);
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

        var chargingApi = CreateChargingModuleApiForContext(context);
        var reservationService = new Mock<IReservationService>();
        var promotionService = new Mock<IPromotionService>();
        promotionService.Setup(s => s.GetUserPromotionsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(ServiceResult<List<UserPromotionDto>>.Ok(new List<UserPromotionDto>()));
        var sut = new ChargingSessionService(chargingApi.Object, reservationService.Object, new PricingService(chargingApi.Object), promotionService.Object);

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
            CompanyId = null
        };
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static Mock<SC.IChargingModuleApi> CreateChargingModuleApiForContext(AppDbContext context)
    {
        var mock = new Mock<SC.IChargingModuleApi>();
        SC.ReservationContract MapReservation(Reservation r)
        {
            var station = context.ChargingStations.FirstOrDefault(s => s.Id == r.ChargingStationId);
            return new SC.ReservationContract
            {
                Id = r.Id,
                UserId = r.UserId,
                ChargingStationId = r.ChargingStationId,
                StartTimeUtc = r.StartTime,
                EndTimeUtc = r.EndTime,
                ExpiresAtUtc = r.ExpiresAtUtc,
                CancelledAtUtc = r.CancelledAtUtc,
                EstimatedCost = r.EstimatedCost,
                Status = r.Status switch
                {
                    EReservationStatus.Active => SC.EReservationStatus.Active,
                    EReservationStatus.Cancelled => SC.EReservationStatus.Cancelled,
                    EReservationStatus.Expired => SC.EReservationStatus.Expired,
                    EReservationStatus.Started => SC.EReservationStatus.Started,
                    _ => SC.EReservationStatus.Active
                },
                PromotionId = r.PromotionId,
                StationName = station?.Name.Translate() ?? station?.Name.ToString() ?? string.Empty
            };
        }

        SC.ChargingSessionContract MapSession(ChargingSession s)
        {
            var station = context.ChargingStations.FirstOrDefault(cs => cs.Id == s.ChargingStationId);
            var promotion = s.PromotionId.HasValue ? context.Promotions.FirstOrDefault(p => p.Id == s.PromotionId.Value) : null;
            return new SC.ChargingSessionContract
            {
                Id = s.Id,
                UserId = s.UserId,
                ChargingStationId = s.ChargingStationId,
                ReservationId = s.ReservationId,
                PromotionId = s.PromotionId,
                StartTimeUtc = s.StartTime,
                EndTimeUtc = s.EndTime,
                EnergyConsumed = s.EnergyConsumed,
                Cost = s.Cost,
                StationName = station?.Name.Translate() ?? station?.Name.ToString() ?? string.Empty,
                StationPricePerKwh = station?.PricePerKwh ?? 0m,
                StationMaxPower = station?.MaxPower,
                PromotionCode = promotion?.Code,
                PromotionDiscountValue = promotion?.DiscountValue
            };
        }

        mock.Setup(x => x.GetStationByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid stationId, CancellationToken _) =>
            {
                var station = context.ChargingStations.FirstOrDefault(s => s.Id == stationId);
                if (station == null) return null;

                return new SC.ChargingStationContract
                {
                    Id = station.Id,
                    Name = station.Name.Translate() ?? station.Name.ToString() ?? string.Empty,
                    Location = station.Location,
                    Status = station.Status switch
                    {
                        EStationStatus.Available => SC.EStationStatus.Available,
                        EStationStatus.InUse => SC.EStationStatus.InUse,
                        EStationStatus.Maintenance => SC.EStationStatus.Maintenance,
                        _ => SC.EStationStatus.Available
                    },
                    PricePerKwh = station.PricePerKwh,
                    MaxPower = station.MaxPower,
                    IsActive = station.IsActive,
                    CompanyId = station.CompanyId
                };
            });

        mock.Setup(x => x.GetReservationByIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid reservationId, Guid userId, CancellationToken _) =>
            {
                var reservation = context.Reservations.FirstOrDefault(r => r.Id == reservationId && r.UserId == userId);
                return reservation == null ? null : MapReservation(reservation);
            });

        mock.Setup(x => x.UpdateReservationStatusAsync(
                It.IsAny<Guid>(),
                It.IsAny<SC.EReservationStatus>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<SC.EStationStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid reservationId, SC.EReservationStatus status, DateTime? expiresAtUtc, DateTime? cancelledAtUtc, SC.EStationStatus? stationStatus, CancellationToken _) =>
            {
                var reservation = context.Reservations.FirstOrDefault(r => r.Id == reservationId);
                if (reservation == null) return false;

                reservation.Status = status switch
                {
                    SC.EReservationStatus.Active => EReservationStatus.Active,
                    SC.EReservationStatus.Cancelled => EReservationStatus.Cancelled,
                    SC.EReservationStatus.Expired => EReservationStatus.Expired,
                    SC.EReservationStatus.Started => EReservationStatus.Started,
                    _ => EReservationStatus.Active
                };
                if (expiresAtUtc.HasValue) reservation.ExpiresAtUtc = expiresAtUtc.Value;
                reservation.CancelledAtUtc = cancelledAtUtc;
                if (stationStatus.HasValue)
                {
                    var station = context.ChargingStations.FirstOrDefault(s => s.Id == reservation.ChargingStationId);
                    if (station != null)
                    {
                        station.Status = stationStatus.Value switch
                        {
                            SC.EStationStatus.Available => EStationStatus.Available,
                            SC.EStationStatus.InUse => EStationStatus.InUse,
                            SC.EStationStatus.Maintenance => EStationStatus.Maintenance,
                            _ => EStationStatus.Available
                        };
                    }
                }

                context.SaveChanges();
                return true;
            });

        mock.Setup(x => x.GetChargingSessionByReservationIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid reservationId, CancellationToken _) =>
            {
                var session = context.ChargingSessions.FirstOrDefault(s => s.ReservationId == reservationId);
                return session == null ? null : MapSession(session);
            });

        mock.Setup(x => x.CreateChargingSessionAsync(It.IsAny<SC.ChargingSessionContract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SC.ChargingSessionContract contract, CancellationToken _) =>
            {
                context.ChargingSessions.Add(new ChargingSession
                {
                    Id = contract.Id,
                    UserId = contract.UserId,
                    ChargingStationId = contract.ChargingStationId,
                    ReservationId = contract.ReservationId,
                    PromotionId = contract.PromotionId,
                    StartTime = contract.StartTimeUtc,
                    EndTime = contract.EndTimeUtc,
                    EnergyConsumed = contract.EnergyConsumed,
                    Cost = contract.Cost
                });
                context.SaveChanges();
                return MapSession(context.ChargingSessions.First(s => s.Id == contract.Id));
            });

        mock.Setup(x => x.GetChargingSessionByIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid sessionId, Guid userId, CancellationToken _) =>
            {
                var session = context.ChargingSessions.FirstOrDefault(s => s.Id == sessionId && s.UserId == userId);
                return session == null ? null : MapSession(session);
            });

        mock.Setup(x => x.GetChargingSessionByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid sessionId, CancellationToken _) =>
            {
                var session = context.ChargingSessions.FirstOrDefault(s => s.Id == sessionId);
                return session == null ? null : MapSession(session);
            });

        mock.Setup(x => x.GetUserChargingSessionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, CancellationToken _) =>
                context.ChargingSessions.Where(s => s.UserId == userId).Select(MapSession).ToList().AsReadOnly());

        mock.Setup(x => x.CompleteChargingSessionAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<Guid?>(),
                It.IsAny<SC.EStationStatus>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid sessionId, DateTime endTimeUtc, decimal energy, decimal cost, Guid? promotionId, SC.EStationStatus stationStatus, CancellationToken _) =>
            {
                var session = context.ChargingSessions.FirstOrDefault(s => s.Id == sessionId);
                if (session == null) return false;
                session.EndTime = endTimeUtc;
                session.EnergyConsumed = energy;
                session.Cost = cost;
                session.PromotionId = promotionId;
                var station = context.ChargingStations.FirstOrDefault(s => s.Id == session.ChargingStationId);
                if (station != null)
                {
                    station.Status = stationStatus switch
                    {
                        SC.EStationStatus.Available => EStationStatus.Available,
                        SC.EStationStatus.InUse => EStationStatus.InUse,
                        SC.EStationStatus.Maintenance => EStationStatus.Maintenance,
                        _ => EStationStatus.Available
                    };
                }

                context.SaveChanges();
                return true;
            });
        return mock;
    }
}
