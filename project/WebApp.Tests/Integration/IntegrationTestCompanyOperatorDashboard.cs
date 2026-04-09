using System.Net;
using AngleSharp.Html.Dom;
using App.DAL.EF;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Tests.Helpers;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestCompanyOperatorDashboard : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public IntegrationTestCompanyOperatorDashboard(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Dashboard_Index_CompanyOwnerWithMembership_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, companyId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var response = await client.GetAsync($"/Company/Dashboard/Index?companyId={companyId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_Index_ForeignCompany_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var foreignCompanyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, companyId);
        await SeedCompany(authFactory, foreignCompanyId, "foreign-company");

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var response = await client.GetAsync($"/Company/Dashboard/Index?companyId={foreignCompanyId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Maintenance_Create_ReportsIssue_AndRedirects()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, companyId, stationId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var getCreate = await client.GetAsync($"/Company/Maintenance/Create?companyId={companyId}");
        Assert.Equal(HttpStatusCode.OK, getCreate.StatusCode);

        var createDoc = await HtmlHelpers.GetDocumentAsync(getCreate);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(createDoc.QuerySelector("form"));
        var submit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(form.QuerySelectorAll("button[type=submit]")));
        var post = await client.SendAsync(form, submit, new Dictionary<string, string>
        {
            ["StationId"] = stationId.ToString(),
            ["IssueDescription"] = "Integration issue report"
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var issue = db.Maintenances.Single(m => m.ChargingStationId == stationId);
        Assert.Equal(EMaintenanceStatus.Reported, issue.Status);
    }

    [Fact]
    public async Task Maintenance_Update_ToResolved_SetsResolvedAtUtc()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var issueId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, companyId, stationId, issueId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var getDetails = await client.GetAsync($"/Company/Maintenance/Details/{issueId}?companyId={companyId}");
        Assert.Equal(HttpStatusCode.OK, getDetails.StatusCode);

        var detailsDoc = await HtmlHelpers.GetDocumentAsync(getDetails);
        var updateForm = Assert.IsAssignableFrom<IHtmlFormElement>(detailsDoc.QuerySelector("form[action*='Update']"));
        var updateSubmit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(updateForm.QuerySelectorAll("button[type=submit]")));
        var post = await client.SendAsync(updateForm, updateSubmit, new Dictionary<string, string>
        {
            ["status"] = EMaintenanceStatus.Resolved.ToString(),
            ["notes"] = "Resolved in integration test"
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var issue = db.Maintenances.Single(m => m.Id == issueId);
        Assert.Equal(EMaintenanceStatus.Resolved, issue.Status);
        Assert.NotNull(issue.ResolvedAt);
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

    private static async Task SeedCompanyOwnerData(WebApplicationFactory<Program> factory, Guid userId, Guid companyId, Guid? stationId = null, Guid? issueId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!db.Users.Any(u => u.Id == userId))
        {
            db.Users.Add(new AppUser { Id = userId, UserName = $"owner-{userId}", Email = $"owner-{userId}@test.local" });
        }

        if (!db.Companies.Any(c => c.Id == companyId))
        {
            db.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Company",
                ContactEmail = "company@test.local",
                ContactPhone = "+3726000000",
                Slug = $"company-{companyId:N}",
                IsActive = true
            });
        }

        if (!db.AppUserCompanies.Any(uc => uc.AppUserId == userId && uc.CompanyId == companyId))
        {
            db.AppUserCompanies.Add(new AppUserCompany
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                CompanyId = companyId,
                Role = ECompanyRole.Owner,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            });
        }

        if (stationId.HasValue && !db.ChargingStations.Any(s => s.Id == stationId.Value))
        {
            db.ChargingStations.Add(new ChargingStation
            {
                Id = stationId.Value,
                Name = new LangStr("Operator Station"),
                Location = "Tallinn",
                Status = EStationStatus.Available,
                PricePerKwh = 0.35m,
                MaxPower = 150m,
                IsActive = true,
                CompanyId = companyId
            });
        }

        if (issueId.HasValue && stationId.HasValue && !db.Maintenances.Any(m => m.Id == issueId.Value))
        {
            db.Maintenances.Add(new Maintenance
            {
                Id = issueId.Value,
                ChargingStationId = stationId.Value,
                ReportedByUserId = userId,
                IssueDescription = "Existing issue",
                Status = EMaintenanceStatus.Reported,
                ReportedAt = DateTime.UtcNow.AddMinutes(-10)
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedCompany(WebApplicationFactory<Program> factory, Guid companyId, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Companies.Any(c => c.Id == companyId))
        {
            return;
        }

        db.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Foreign",
            ContactEmail = "foreign@test.local",
            ContactPhone = "+3727000000",
            Slug = slug,
            IsActive = true
        });

        await db.SaveChangesAsync();
    }
}
