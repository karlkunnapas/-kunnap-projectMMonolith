using System.Net;
using AngleSharp.Html.Dom;
using App.DAL.EF;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Tests.Helpers;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestAdminPanel : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task AdminDashboard_AdminRole_ReturnsOk()
    {
        await using var authFactory = CreateAuthenticatedFactory();
        var adminUserId = Guid.NewGuid();
        await SeedUser(authFactory, adminUserId, "admin@test.local");

        var client = CreateAuthenticatedClient(authFactory, adminUserId, "Admin");
        var response = await client.GetAsync("/Admin/Dashboard/Index");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminCompanies_Inactivate_Post_UpdatesCompany()
    {
        await using var authFactory = CreateAuthenticatedFactory();
        var adminUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await SeedUser(authFactory, adminUserId, "admin@test.local");
        await SeedCompany(authFactory, companyId, "company-to-disable");

        var client = CreateAuthenticatedClient(authFactory, adminUserId, "Admin");
        var get = await client.GetAsync("/Admin/Companies/Index");
        var document = await HtmlHelpers.GetDocumentAsync(get);
        var targetInput = Assert.IsAssignableFrom<IHtmlInputElement>(
            document.QuerySelectorAll("input[name='companyId']").First(i => i.GetAttribute("value") == companyId.ToString()));
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(targetInput.Form);
        var submit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(form.QuerySelectorAll("button[type=submit]")));

        var post = await client.SendAsync(form, submit, new Dictionary<string, string>
        {
            ["companyId"] = companyId.ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(db.Companies.IgnoreQueryFilters().Single(c => c.Id == companyId).IsActive);
    }

    [Fact]
    public async Task AdminAuditLogs_FilterByActor_ReturnsFilteredRows()
    {
        await using var authFactory = CreateAuthenticatedFactory();
        var adminUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await SeedUser(authFactory, adminUserId, "admin@test.local");
        await SeedCompany(authFactory, companyId, "audit-company");
        await SeedAuditLog(authFactory, companyId, "alpha.admin@test.local", "CompanyActivated");
        await SeedAuditLog(authFactory, companyId, "beta.admin@test.local", "CompanyInactivated");

        var client = CreateAuthenticatedClient(authFactory, adminUserId, "Admin");
        var response = await client.GetAsync("/Admin/AuditLogs/Index?actor=alpha.admin");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("alpha.admin@test.local", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("beta.admin@test.local", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminStations_Index_ReturnsStationRows()
    {
        await using var authFactory = CreateAuthenticatedFactory();
        var adminUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await SeedUser(authFactory, adminUserId, "admin@test.local");
        await SeedCompany(authFactory, companyId, "stations-company");
        await SeedStation(authFactory, companyId, "Admin Station");

        var client = CreateAuthenticatedClient(authFactory, adminUserId, "Admin");
        var response = await client.GetAsync("/Admin/Stations/Index");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Admin Station", html, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<Program> CreateAuthenticatedFactory()
    {
        return new CustomWebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                });
            });
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, Guid userId, params string[] roles)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    private static async Task SeedUser(WebApplicationFactory<Program> factory, Guid userId, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Users.Any(u => u.Id == userId))
        {
            return;
        }

        db.Users.Add(new AppUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedCompany(WebApplicationFactory<Program> factory, Guid companyId, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Companies.IgnoreQueryFilters().Any(c => c.Id == companyId))
        {
            return;
        }

        db.Companies.Add(new Company
        {
            Id = companyId,
            Name = new LangStr("Integration Company"),
            ContactEmail = "company@test.local",
            ContactPhone = "+3725550000",
            Slug = slug,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedAuditLog(WebApplicationFactory<Program> factory, Guid companyId, string userName, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserName = userName,
            EntityName = nameof(Company),
            EntityId = companyId,
            Action = action,
            AtUtc = DateTime.UtcNow,
            ChangesJson = "{}"
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedStation(WebApplicationFactory<Program> factory, Guid companyId, string stationName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChargingStations.Add(new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr(stationName),
            Location = "Tallinn",
            Status = EStationStatus.Available,
            PricePerKwh = 0.25m,
            MaxPower = 90m,
            IsActive = true,
            CompanyId = companyId
        });
        await db.SaveChangesAsync();
    }
}
