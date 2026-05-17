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
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts;
using WebApp.Tests.Helpers;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestRootStationIssueReporting : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task Station_ReportIssue_Customer_CreatesMaintenanceIssue()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedData(authFactory, userId, companyId, stationId);

        var client = CreateAuthenticatedClient(authFactory, userId, "Customer");
        var get = await client.GetAsync($"/Root/Station/Details/{stationId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var doc = await HtmlHelpers.GetDocumentAsync(get);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(doc.QuerySelector("form[action*='ReportIssue']"));
        var submitButton = Assert.IsAssignableFrom<IHtmlButtonElement>(form.QuerySelector("button[type='submit']"));
        Assert.Contains("station-button-compact", submitButton.ClassName);
        var detailCards = doc.QuerySelectorAll(".reservation-section-card").ToList();
        var lastCard = Assert.Single(detailCards.Skip(detailCards.Count - 1));
        Assert.NotNull(lastCard.QuerySelector("form[action*='ReportIssue']"));
        var post = await PostFormAsync(client, form, new Dictionary<string, string>
        {
            ["IssueReportForm.StationId"] = stationId.ToString(),
            ["IssueReportForm.IssueDescription"] = "Cable damage near connector"
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var chargingDb = scope.ServiceProvider.GetRequiredService<ChargingDbContext>();
        var issue = chargingDb.Maintenances.Single(m => m.ChargingStationId == stationId);
        Assert.Equal(EMaintenanceStatus.Reported, issue.Status);
        var station = chargingDb.ChargingStations.Single(s => s.Id == stationId);
        Assert.Equal(EStationStatus.Available, station.Status);
    }

    [Fact]
    public async Task Station_ReportIssue_CompanyOwner_DoesNotSeeReportOption()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedData(authFactory, userId, companyId, stationId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var get = await client.GetAsync($"/Root/Station/Details/{stationId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var doc = await HtmlHelpers.GetDocumentAsync(get);
        Assert.Null(doc.QuerySelector("form[action*='ReportIssue']"));
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

    private static async Task SeedData(WebApplicationFactory<Program> factory, Guid userId, Guid companyId, Guid stationId)
    {
        using var scope = factory.Services.CreateScope();
        var usersDb = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var companiesDb = scope.ServiceProvider.GetRequiredService<CompaniesDbContext>();
        var chargingDb = scope.ServiceProvider.GetRequiredService<ChargingDbContext>();

        if (!usersDb.Users.Any(u => u.Id == userId))
        {
            usersDb.Users.Add(new AppUser { Id = userId, UserName = $"user-{userId}", Email = $"user-{userId}@test.local" });
        }

        if (!companiesDb.Companies.Any(c => c.Id == companyId))
        {
            companiesDb.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Ops Company",
                ContactEmail = "ops@test.local",
                ContactPhone = "+3725550000",
                Slug = $"ops-{companyId:N}",
                IsActive = true
            });
        }

        if (!chargingDb.ChargingStations.Any(s => s.Id == stationId))
        {
            chargingDb.ChargingStations.Add(new ChargingStation
            {
                Id = stationId,
                Name = new LangStr("Customer visible station"),
                Location = "Tallinn",
                Status = EStationStatus.Available,
                PricePerKwh = 0.35m,
                MaxPower = 150m,
                IsActive = true,
                CompanyId = companyId
            });
        }

        await usersDb.SaveChangesAsync();
        await companiesDb.SaveChangesAsync();
        await chargingDb.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        IHtmlFormElement form,
        Dictionary<string, string> values)
    {
        var tokenValue = (form.QuerySelector("input[name='__RequestVerificationToken']") as IHtmlInputElement)?.Value;
        if (!string.IsNullOrWhiteSpace(tokenValue))
        {
            values["__RequestVerificationToken"] = tokenValue;
        }

        return client.PostAsync(form.Action, new FormUrlEncodedContent(values));
    }
}
