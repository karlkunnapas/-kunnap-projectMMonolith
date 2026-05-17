using App.BLL.Services;
using Moq;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;

namespace WebApp.Tests.Unit;

public class UnitTestPromotionService
{
    [Fact]
    public async Task ValidateUserPromotionAsync_OtherCompanyStation_ReturnsClearMismatchError()
    {
        var userId = Guid.NewGuid();
        var promoCompanyId = Guid.NewGuid();
        var stationCompanyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();
        var companiesApi = new Mock<ICompaniesModuleApi>();
        var chargingApi = new Mock<IChargingModuleApi>();
        chargingApi.Setup(x => x.GetStationByIdAsync(stationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChargingStationContract { Id = stationId, CompanyId = stationCompanyId });
        companiesApi
            .Setup(x => x.GetValidUserPromotionByCodeAsync(userId, "COMPANY50", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Shared.Contracts.Companies.UserPromotionContract
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PromotionId = promotionId,
                AddedAtUtc = DateTime.UtcNow,
                IsUsed = false,
                Promotion = new CompanyPromotionContract
                {
                    Id = promotionId,
                    Code = "COMPANY50",
                    DiscountValue = 50m,
                    ValidFromUtc = DateTime.UtcNow.AddDays(-1),
                    ValidToUtc = DateTime.UtcNow.AddDays(5),
                    IsActive = true,
                    CompanyId = promoCompanyId
                }
            });
        var service = new PromotionService(chargingApi.Object, companiesApi.Object);

        var result = await service.ValidateUserPromotionAsync(userId, stationId, "COMPANY50");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "PROMOTION_COMPANY_MISMATCH");
    }

    [Fact]
    public async Task ValidateUserPromotionAsync_SystemPromotion_WorksOnAnyCompanyStation()
    {
        var userId = Guid.NewGuid();
        var stationCompanyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();
        var companiesApi = new Mock<ICompaniesModuleApi>();
        var chargingApi = new Mock<IChargingModuleApi>();
        chargingApi.Setup(x => x.GetStationByIdAsync(stationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChargingStationContract { Id = stationId, CompanyId = stationCompanyId });
        companiesApi
            .Setup(x => x.GetValidUserPromotionByCodeAsync(userId, "SYSTEM10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Shared.Contracts.Companies.UserPromotionContract
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PromotionId = promotionId,
                AddedAtUtc = DateTime.UtcNow,
                IsUsed = false,
                Promotion = new CompanyPromotionContract
                {
                    Id = promotionId,
                    Code = "SYSTEM10",
                    DiscountValue = 10m,
                    ValidFromUtc = DateTime.UtcNow.AddDays(-1),
                    ValidToUtc = DateTime.UtcNow.AddDays(5),
                    IsActive = true,
                    CompanyId = null
                }
            });
        var service = new PromotionService(chargingApi.Object, companiesApi.Object);

        var result = await service.ValidateUserPromotionAsync(userId, stationId, "SYSTEM10");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(10m, result.Data!.DiscountValue);
    }

    [Fact]
    public async Task ValidateUserPromotionAsync_SystemPromotion_WorksOnStationWithoutCompany()
    {
        var userId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();
        var companiesApi = new Mock<ICompaniesModuleApi>();
        var chargingApi = new Mock<IChargingModuleApi>();
        chargingApi.Setup(x => x.GetStationByIdAsync(stationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChargingStationContract { Id = stationId, CompanyId = null });
        companiesApi
            .Setup(x => x.GetValidUserPromotionByCodeAsync(userId, "SYSTEM15", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Shared.Contracts.Companies.UserPromotionContract
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PromotionId = promotionId,
                AddedAtUtc = DateTime.UtcNow,
                IsUsed = false,
                Promotion = new CompanyPromotionContract
                {
                    Id = promotionId,
                    Code = "SYSTEM15",
                    DiscountValue = 15m,
                    ValidFromUtc = DateTime.UtcNow.AddDays(-1),
                    ValidToUtc = DateTime.UtcNow.AddDays(5),
                    IsActive = true,
                    CompanyId = null
                }
            });
        var service = new PromotionService(chargingApi.Object, companiesApi.Object);

        var result = await service.ValidateUserPromotionAsync(userId, stationId, "SYSTEM15");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(15m, result.Data!.DiscountValue);
    }
}
