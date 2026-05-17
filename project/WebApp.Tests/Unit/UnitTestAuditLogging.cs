using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Modules.Companies.Domain;
using Modules.Companies.Infrastructure;
using Shared.Contracts;

namespace WebApp.Tests.Unit;

public class UnitTestAuditLogging
{
    private static CompaniesDbContext CreateContext(string? userName = null)
    {
        var options = new DbContextOptionsBuilder<CompaniesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var httpContext = new DefaultHttpContext();
        if (!string.IsNullOrWhiteSpace(userName))
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, userName)
            ], "UnitTestAuth"));
        }

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var ctx = new CompaniesDbContext(options, accessor);
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
            Name = new LangStr { ["en"] = "Unit Test Company" },
            ContactEmail = "unit@company.test",
            ContactPhone = "+3720000000",
            Slug = "unit-test-company",
            IsActive = true
        };

        ctx.Companies.Add(company);
        ctx.SaveChanges();

        var log = ctx.AuditLogs.Single(l => l.EntityId == company.Id && l.Action == "Created");

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
            Name = new LangStr { ["en"] = "Audit Diff Company" },
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

        var updateLog = ctx.AuditLogs.Single(l => l.EntityId == promotion.Id && l.Action == "Updated");
        var deleteLog = ctx.AuditLogs.Single(l => l.EntityId == promotion.Id && l.Action == "Deleted");

        Assert.NotNull(updateLog.ChangesJson);
        Assert.NotNull(deleteLog.ChangesJson);

        using var updateDoc = JsonDocument.Parse(updateLog.ChangesJson!);
        Assert.True(updateDoc.RootElement.TryGetProperty(nameof(Promotion.Code), out var codeDiff));
        Assert.Equal("SPRING-UNIT", codeDiff.GetProperty("Old").GetString());
        Assert.Equal("SPRING-UNIT-UPDATED", codeDiff.GetProperty("New").GetString());
    }
}
