using Modules.Companies.Domain;
using Modules.Companies.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestAuditLogging : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public IntegrationTestAuditLogging(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SaveChanges_WritesAuditLogs_ThroughApplicationServiceProvider()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CompaniesDbContext>();

        var company = new Company
        {
            Name = "Integration Audit Company",
            ContactEmail = "integration@company.test",
            ContactPhone = "+3723333333",
            Slug = "integration-audit-company",
            IsActive = true
        };

        ctx.Companies.Add(company);
        await ctx.SaveChangesAsync();

        company.ContactEmail = "integration-updated@company.test";
        await ctx.SaveChangesAsync();

        var logs = ctx.AuditLogs
            .Where(l => l.EntityName == nameof(Company) && l.EntityId == company.Id)
            .OrderBy(l => l.AtUtc)
            .ToList();

        Assert.Contains(logs, l => l.Action == "Created");
        Assert.Contains(logs, l => l.Action == "Updated");
        Assert.All(logs, l => Assert.Equal(company.Id, l.CompanyId));
        Assert.All(logs, l => Assert.Equal("system", l.UserName));
    }
}
