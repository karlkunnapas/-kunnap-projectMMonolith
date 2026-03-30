using System.Text.Json;
using App.DAL.EF;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Tests.Unit;

public class UnitTestAuditLogging
{
    private static AppDbContext CreateContext(string? userName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var provider = new TestAuditActorProvider(userName);
        var ctx = new AppDbContext(options, provider);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public void SaveChanges_CreatesAuditLog_ForCompanyCreate()
    {
        using var ctx = CreateContext("unit.user@example.com");

        var company = new Company
        {
            Name = "Unit Test Company",
            ContactEmail = "unit@company.test",
            ContactPhone = "+3720000000",
            Slug = "unit-test-company",
            IsActive = true
        };

        ctx.Companies.Add(company);
        ctx.SaveChanges();

        var log = ctx.AuditLogs.Single(l => l.EntityId == company.Id && l.Action == "Create");

        Assert.Equal(company.Id, log.CompanyId);
        Assert.Equal("unit.user@example.com", log.UserName);
        Assert.Equal(nameof(Company), log.EntityName);
        Assert.NotNull(log.ChangesJson);
    }

    [Fact]
    public async Task SaveChangesAsync_CreatesUpdateAndDeleteAuditLogs_WithDiffs()
    {
        await using var ctx = CreateContext("unit.user@example.com");

        var company = new Company
        {
            Name = "Audit Diff Company",
            ContactEmail = "audit@company.test",
            ContactPhone = "+3721111111",
            Slug = "audit-diff-company",
            IsActive = true
        };

        ctx.Companies.Add(company);
        await ctx.SaveChangesAsync();

        var promotion = new Promotion
        {
            Code = "SPRING-UNIT",
            DiscountValue = 10,
            ValidFrom = DateTime.UtcNow,
            ValidTo = DateTime.UtcNow.AddDays(7),
            IsActive = true,
            CompanyId = company.Id
        };

        ctx.Promotions.Add(promotion);
        await ctx.SaveChangesAsync();

        promotion.Code = "SPRING-UNIT-UPDATED";
        await ctx.SaveChangesAsync();

        ctx.Promotions.Remove(promotion);
        await ctx.SaveChangesAsync();

        var updateLog = ctx.AuditLogs.Single(l => l.EntityId == promotion.Id && l.Action == "Update");
        var deleteLog = ctx.AuditLogs.Single(l => l.EntityId == promotion.Id && l.Action == "Delete");

        Assert.NotNull(updateLog.ChangesJson);
        Assert.NotNull(deleteLog.ChangesJson);

        using var updateDoc = JsonDocument.Parse(updateLog.ChangesJson!);
        var changedPhone = updateDoc.RootElement
            .EnumerateArray()
            .Single(e => e.GetProperty("property").GetString() == nameof(Promotion.Code));

        Assert.Equal("SPRING-UNIT", changedPhone.GetProperty("old").GetString());
        Assert.Equal("SPRING-UNIT-UPDATED", changedPhone.GetProperty("new").GetString());
    }

    private sealed class TestAuditActorProvider(string? userName) : IAuditActorProvider
    {
        public string? UserName { get; } = userName;
    }
}


