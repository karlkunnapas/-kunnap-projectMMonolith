using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using App.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shared.Contracts.Companies;

namespace WebApp.Tests.Unit;

public class UnitTestPromotionService
{
    [Fact]
    public async Task ValidateUserPromotionAsync_OtherCompanyStation_ReturnsClearMismatchError()
    {
        await using var context = BuildContext();
        var userId = Guid.NewGuid();
        var promoCompanyId = Guid.NewGuid();
        var stationCompanyId = Guid.NewGuid();
        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            Code = "COMPANY50",
            DiscountValue = 50m,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(5),
            IsActive = true,
            CompanyId = promoCompanyId
        };
        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("Station A"),
            Location = "Tallinn",
            Status = EStationStatus.Available,
            PricePerKwh = 0.4m,
            MaxPower = 50m,
            IsActive = true,
            CompanyId = stationCompanyId
        };

        context.Users.Add(new AppUser { Id = userId, UserName = $"user-{userId}", Email = $"user-{userId}@test.local" });
        context.Companies.AddRange(
            new Company { Id = promoCompanyId, Name = "PromoCo", ContactEmail = "p@test.local", ContactPhone = "+3721000001", Slug = $"p-{Guid.NewGuid():N}", IsActive = true },
            new Company { Id = stationCompanyId, Name = "StationCo", ContactEmail = "s@test.local", ContactPhone = "+3721000002", Slug = $"s-{Guid.NewGuid():N}", IsActive = true }
        );
        context.ChargingStations.Add(station);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var companiesApi = new Mock<ICompaniesModuleApi>();
        companiesApi
            .Setup(x => x.GetValidUserPromotionByCodeAsync(userId, promotion.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Shared.Contracts.Companies.UserPromotionContract
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PromotionId = promotion.Id,
                AddedAtUtc = DateTime.UtcNow,
                IsUsed = false,
                Promotion = new CompanyPromotionContract
                {
                    Id = promotion.Id,
                    Code = promotion.Code,
                    DiscountValue = promotion.DiscountValue,
                    ValidFromUtc = promotion.ValidFrom,
                    ValidToUtc = promotion.ValidTo,
                    IsActive = true,
                    CompanyId = promoCompanyId
                }
            });
        var service = new PromotionService(uow, companiesApi.Object);

        var result = await service.ValidateUserPromotionAsync(userId, station.Id, promotion.Code);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "PROMOTION_COMPANY_MISMATCH");
    }

    [Fact]
    public async Task ValidateUserPromotionAsync_SystemPromotion_WorksOnAnyCompanyStation()
    {
        await using var context = BuildContext();
        var userId = Guid.NewGuid();
        var stationCompanyId = Guid.NewGuid();
        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            Code = "SYSTEM10",
            DiscountValue = 10m,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(5),
            IsActive = true,
            CompanyId = null
        };
        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("Station B"),
            Location = "Tartu",
            Status = EStationStatus.Available,
            PricePerKwh = 0.4m,
            MaxPower = 50m,
            IsActive = true,
            CompanyId = stationCompanyId
        };

        context.Users.Add(new AppUser { Id = userId, UserName = $"user-{userId}", Email = $"user-{userId}@test.local" });
        context.Companies.Add(new Company
        {
            Id = stationCompanyId,
            Name = "StationCo",
            ContactEmail = "s@test.local",
            ContactPhone = "+3721000003",
            Slug = $"sc-{Guid.NewGuid():N}",
            IsActive = true
        });
        context.ChargingStations.Add(station);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var companiesApi = new Mock<ICompaniesModuleApi>();
        companiesApi
            .Setup(x => x.GetValidUserPromotionByCodeAsync(userId, promotion.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Shared.Contracts.Companies.UserPromotionContract
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PromotionId = promotion.Id,
                AddedAtUtc = DateTime.UtcNow,
                IsUsed = false,
                Promotion = new CompanyPromotionContract
                {
                    Id = promotion.Id,
                    Code = promotion.Code,
                    DiscountValue = promotion.DiscountValue,
                    ValidFromUtc = promotion.ValidFrom,
                    ValidToUtc = promotion.ValidTo,
                    IsActive = true,
                    CompanyId = null
                }
            });
        var service = new PromotionService(uow, companiesApi.Object);

        var result = await service.ValidateUserPromotionAsync(userId, station.Id, promotion.Code);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(10m, result.Data!.DiscountValue);
    }

    [Fact]
    public async Task ValidateUserPromotionAsync_SystemPromotion_WorksOnStationWithoutCompany()
    {
        await using var context = BuildContext();
        var userId = Guid.NewGuid();
        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            Code = "SYSTEM15",
            DiscountValue = 15m,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidTo = DateTime.UtcNow.AddDays(5),
            IsActive = true,
            CompanyId = null
        };
        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("Station C"),
            Location = "Parnu",
            Status = EStationStatus.Available,
            PricePerKwh = 0.4m,
            MaxPower = 50m,
            IsActive = true,
            CompanyId = null
        };

        context.Users.Add(new AppUser { Id = userId, UserName = $"user-{userId}", Email = $"user-{userId}@test.local" });
        context.ChargingStations.Add(station);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var companiesApi = new Mock<ICompaniesModuleApi>();
        companiesApi
            .Setup(x => x.GetValidUserPromotionByCodeAsync(userId, promotion.Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Shared.Contracts.Companies.UserPromotionContract
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PromotionId = promotion.Id,
                AddedAtUtc = DateTime.UtcNow,
                IsUsed = false,
                Promotion = new CompanyPromotionContract
                {
                    Id = promotion.Id,
                    Code = promotion.Code,
                    DiscountValue = promotion.DiscountValue,
                    ValidFromUtc = promotion.ValidFrom,
                    ValidToUtc = promotion.ValidTo,
                    IsActive = true,
                    CompanyId = null
                }
            });
        var service = new PromotionService(uow, companiesApi.Object);

        var result = await service.ValidateUserPromotionAsync(userId, station.Id, promotion.Code);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(15m, result.Data!.DiscountValue);
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
