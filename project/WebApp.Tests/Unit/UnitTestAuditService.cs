using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Tests.Unit;

public class UnitTestAuditService
{
    [Fact]
    public async Task GetAuditTrailAsync_ReturnsChronologicalEntriesForEntity()
    {
        await using var context = BuildContext();
        var companyId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        context.AuditLogs.AddRange(
            new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                EntityName = nameof(ChargingSession),
                EntityId = entityId,
                Action = "Create",
                UserName = "u1",
                AtUtc = DateTime.UtcNow.AddMinutes(-10),
                ChangesJson = "[]"
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                EntityName = nameof(ChargingSession),
                EntityId = entityId,
                Action = "Update",
                UserName = "u2",
                AtUtc = DateTime.UtcNow.AddMinutes(-5),
                ChangesJson = "[]"
            });

        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var sut = new AuditService(uow);

        var result = await sut.GetAuditTrailAsync(nameof(ChargingSession), entityId, companyId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Entries.Count);
        Assert.Equal("Create", result.Data.Entries[0].Action);
        Assert.Equal("Update", result.Data.Entries[1].Action);
    }

    [Fact]
    public async Task GetCompanyAuditAsync_AppliesEntityAndActionFilters()
    {
        await using var context = BuildContext();
        var companyId = Guid.NewGuid();

        context.AuditLogs.AddRange(
            new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                EntityName = nameof(Reservation),
                EntityId = Guid.NewGuid(),
                Action = "Create",
                UserName = "u1",
                AtUtc = DateTime.UtcNow.AddMinutes(-30),
                ChangesJson = "[]"
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                EntityName = nameof(ChargingSession),
                EntityId = Guid.NewGuid(),
                Action = "Update",
                UserName = "u2",
                AtUtc = DateTime.UtcNow.AddMinutes(-20),
                ChangesJson = "[]"
            });

        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var sut = new AuditService(uow);

        var result = await sut.GetCompanyAuditAsync(companyId, entityName: nameof(ChargingSession), action: "Update");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!);
        Assert.Equal(nameof(ChargingSession), result.Data[0].EntityName);
        Assert.Equal("Update", result.Data[0].Action);
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}

