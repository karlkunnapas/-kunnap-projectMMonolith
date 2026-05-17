using System.Net;
using AngleSharp.Html.Dom;
using Modules.Charging.Domain;
using Modules.Charging.Infrastructure;
using Modules.Companies.Domain;
using Modules.Companies.Infrastructure;
using Modules.Users.Domain;
using Modules.Users.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts;
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
    public async Task Dashboard_Index_DeactivatedCompanyMembership_RedirectsToDeactivatedPage()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, companyId);
        await DeactivateCompany(authFactory, companyId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var response = await client.GetAsync($"/Company/Dashboard/Index?companyId={companyId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/CompanyDeactivated", response.Headers.Location?.ToString());
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
    public async Task Maintenance_Create_CompanyOwner_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, companyId, stationId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var getCreate = await client.GetAsync($"/Company/Maintenance/Create?companyId={companyId}");
        Assert.Equal(HttpStatusCode.Forbidden, getCreate.StatusCode);
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
        var updateForms = detailsDoc.QuerySelectorAll("form[action*='Update']").OfType<IHtmlFormElement>().ToList();
        var resolveForm = Assert.Single(
            updateForms,
            form => (form.QuerySelector("input[name='status']") as IHtmlInputElement)?.Value == EMaintenanceStatus.Resolved.ToString());
        var resolveSubmit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(resolveForm.QuerySelectorAll("button[type=submit]")));
        var post = await client.SendAsync(resolveForm, resolveSubmit);

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var chargingDb = scope.ServiceProvider.GetRequiredService<ChargingDbContext>();
        var issue = chargingDb.Maintenances.Single(m => m.Id == issueId);
        Assert.Equal(EMaintenanceStatus.Resolved, issue.Status);
        Assert.NotNull(issue.ResolvedAt);
        var station = chargingDb.ChargingStations.Single(s => s.Id == stationId);
        Assert.Equal(EStationStatus.Available, station.Status);
    }

    [Fact]
    public async Task Maintenance_Update_ToInProgress_SetsStationToMaintenance()
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
        var updateForms = detailsDoc.QuerySelectorAll("form[action*='Update']").OfType<IHtmlFormElement>().ToList();
        var inProgressForm = Assert.Single(
            updateForms,
            form => (form.QuerySelector("input[name='status']") as IHtmlInputElement)?.Value == EMaintenanceStatus.InProgress.ToString());
        var inProgressSubmit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(inProgressForm.QuerySelectorAll("button[type=submit]")));
        var post = await client.SendAsync(inProgressForm, inProgressSubmit);

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var chargingDb = scope.ServiceProvider.GetRequiredService<ChargingDbContext>();
        var issue = chargingDb.Maintenances.Single(m => m.Id == issueId);
        Assert.Equal(EMaintenanceStatus.InProgress, issue.Status);
        var station = chargingDb.ChargingStations.Single(s => s.Id == stationId);
        Assert.Equal(EStationStatus.Maintenance, station.Status);
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
        var usersDb = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var companiesDb = scope.ServiceProvider.GetRequiredService<CompaniesDbContext>();
        var chargingDb = scope.ServiceProvider.GetRequiredService<ChargingDbContext>();

        if (!usersDb.Users.Any(u => u.Id == userId))
        {
            usersDb.Users.Add(new AppUser { Id = userId, UserName = $"owner-{userId}", Email = $"owner-{userId}@test.local" });
        }

        if (!companiesDb.Companies.Any(c => c.Id == companyId))
        {
            companiesDb.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Company",
                ContactEmail = "company@test.local",
                ContactPhone = "+3726000000",
                Slug = $"company-{companyId:N}",
                IsActive = true
            });
        }

        if (!companiesDb.AppUserCompanies.Any(uc => uc.AppUserId == userId && uc.CompanyId == companyId))
        {
            companiesDb.AppUserCompanies.Add(new AppUserCompany
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                CompanyId = companyId,
                Role = ECompanyRole.Owner,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            });
        }

        if (stationId.HasValue && !chargingDb.ChargingStations.Any(s => s.Id == stationId.Value))
        {
            chargingDb.ChargingStations.Add(new ChargingStation
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

        if (issueId.HasValue && stationId.HasValue && !chargingDb.Maintenances.Any(m => m.Id == issueId.Value))
        {
            chargingDb.Maintenances.Add(new Maintenance
            {
                Id = issueId.Value,
                ChargingStationId = stationId.Value,
                ReportedByUserId = userId,
                IssueDescription = "Existing issue",
                Status = EMaintenanceStatus.Reported,
                ReportedAt = DateTime.UtcNow.AddMinutes(-10)
            });
        }

        await usersDb.SaveChangesAsync();
        await companiesDb.SaveChangesAsync();
        await chargingDb.SaveChangesAsync();
    }

    private static async Task SeedCompany(WebApplicationFactory<Program> factory, Guid companyId, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var companiesDb = scope.ServiceProvider.GetRequiredService<CompaniesDbContext>();
        if (companiesDb.Companies.Any(c => c.Id == companyId))
        {
            return;
        }

        companiesDb.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Foreign",
            ContactEmail = "foreign@test.local",
            ContactPhone = "+3727000000",
            Slug = slug,
            IsActive = true
        });

        await companiesDb.SaveChangesAsync();
    }

    private static async Task DeactivateCompany(WebApplicationFactory<Program> factory, Guid companyId)
    {
        using var scope = factory.Services.CreateScope();
        var companiesDb = scope.ServiceProvider.GetRequiredService<CompaniesDbContext>();

        var company = await companiesDb.Companies.IgnoreQueryFilters().FirstAsync(c => c.Id == companyId);
        company.IsActive = false;
        await companiesDb.SaveChangesAsync();
    }
}
