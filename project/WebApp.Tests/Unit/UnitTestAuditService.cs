using App.BLL.Services;
using Mediator;
using Moq;
using Shared.Contracts.Companies;

namespace WebApp.Tests.Unit;

public class UnitTestAuditService
{
    [Fact]
    public async Task GetAuditTrailAsync_ReturnsChronologicalEntriesForEntity()
    {
        var companyId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var companiesApi = new Mock<ICompaniesModuleApi>();
        companiesApi
            .Setup(x => x.GetAuditTrailAsync("ChargingSession", entityId, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyAuditTrailContract
            {
                EntityName = "ChargingSession",
                EntityId = entityId,
                Entries =
                [
                    new CompanyAuditEntryContract { Id = Guid.NewGuid(), CompanyId = companyId, EntityName = "ChargingSession", EntityId = entityId, Action = "Create", UserName = "u1", AtUtc = DateTime.UtcNow.AddMinutes(-10), ChangesJson = "[]" },
                    new CompanyAuditEntryContract { Id = Guid.NewGuid(), CompanyId = companyId, EntityName = "ChargingSession", EntityId = entityId, Action = "Update", UserName = "u2", AtUtc = DateTime.UtcNow.AddMinutes(-5), ChangesJson = "[]" }
                ]
            });
        var sut = new AuditService(new Mock<IMediator>().Object, companiesApi.Object);

        var result = await sut.GetAuditTrailAsync("ChargingSession", entityId, companyId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Entries.Count);
        Assert.Equal("Create", result.Data.Entries[0].Action);
        Assert.Equal("Update", result.Data.Entries[1].Action);
    }

    [Fact]
    public async Task GetCompanyAuditAsync_AppliesEntityAndActionFilters()
    {
        var companyId = Guid.NewGuid();
        var companiesApi = new Mock<ICompaniesModuleApi>();
        companiesApi
            .Setup(x => x.GetCompanyAuditAsync(companyId, null, null, "ChargingSession", "Update", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompanyAuditEntryContract>
            {
                new() { Id = Guid.NewGuid(), CompanyId = companyId, EntityName = "ChargingSession", EntityId = Guid.NewGuid(), Action = "Update", UserName = "u2", AtUtc = DateTime.UtcNow.AddMinutes(-20), ChangesJson = "[]" }
            });
        var sut = new AuditService(new Mock<IMediator>().Object, companiesApi.Object);

        var result = await sut.GetCompanyAuditAsync(companyId, entityName: "ChargingSession", action: "Update");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!);
        Assert.Equal("ChargingSession", result.Data[0].EntityName);
        Assert.Equal("Update", result.Data[0].Action);
    }

}
