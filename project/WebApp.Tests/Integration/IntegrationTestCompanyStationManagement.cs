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
public class IntegrationTestCompanyStationManagement : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task Station_Create_Get_ReturnsFormWithBilingualInputs()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, companyId, connectorId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var response = await client.GetAsync($"/Company/Station/Create?companyId={companyId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await HtmlHelpers.GetDocumentAsync(response);
        Assert.NotNull(document.QuerySelector("input[name='NameEn']"));
        Assert.NotNull(document.QuerySelector("input[name='NameEt']"));
    }

    [Fact]
    public async Task Station_Create_Post_SavesCompanyScopedStation_AndAuditLog()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, companyId, connectorId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var get = await client.GetAsync($"/Company/Station/Create?companyId={companyId}");
        var doc = await HtmlHelpers.GetDocumentAsync(get);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(doc.QuerySelector("form"));

        var post = await PostFormAsync(client, form, new Dictionary<string, string>
        {
            ["CompanyId"] = companyId.ToString(),
            ["NameEn"] = "Operator Station EN",
            ["NameEt"] = "Operaatori jaam ET",
            ["Location"] = "Tallinn",
            ["PricePerKwh"] = "0.39",
            ["MaxPower"] = "120",
            ["Status"] = ((int)EStationStatus.Available).ToString(),
            ["IsActive"] = "true",
            ["SelectedConnectorIds"] = connectorId.ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var chargingDb = scope.ServiceProvider.GetRequiredService<ChargingDbContext>();
        var companiesDb = scope.ServiceProvider.GetRequiredService<CompaniesDbContext>();
        var station = chargingDb.ChargingStations
            .Where(s => s.CompanyId == companyId && s.Location == "Tallinn")
            .OrderByDescending(s => s.Id)
            .FirstOrDefault();

        Assert.NotNull(station);
        Assert.Equal("Operator Station EN", station!.Name.Translate("en"));
        Assert.Equal("Operaatori jaam ET", station.Name.Translate("et"));
        Assert.True(chargingDb.ChargingStationConnectors.Any(link => link.ChargingStationId == station.Id && link.ConnectorId == connectorId));
        Assert.True(companiesDb.AuditLogs.Any(log => log.CompanyId == companyId && log.EntityName == nameof(ChargingStation) && log.EntityId == station.Id));
    }

    [Fact]
    public async Task Station_Edit_Post_UpdatesInfo_AndConnectorAssignments()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var connectorA = Guid.NewGuid();
        var connectorB = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, companyId, connectorA, connectorB, stationId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var get = await client.GetAsync($"/Company/Station/Edit/{stationId}?companyId={companyId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var doc = await HtmlHelpers.GetDocumentAsync(get);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(doc.QuerySelector("form[action*='Edit']"));

        var post = await PostFormAsync(client, form, new Dictionary<string, string>
        {
            ["Id"] = stationId.ToString(),
            ["CompanyId"] = companyId.ToString(),
            ["NameEn"] = "Updated Station EN",
            ["NameEt"] = "Uuendatud jaam ET",
            ["Location"] = "Tartu",
            ["PricePerKwh"] = "0.41",
            ["MaxPower"] = "155",
            ["Status"] = ((int)EStationStatus.InUse).ToString(),
            ["IsActive"] = "true",
            ["SelectedConnectorIds"] = connectorB.ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var chargingDb = scope.ServiceProvider.GetRequiredService<ChargingDbContext>();
        var station = chargingDb.ChargingStations.Single(s => s.Id == stationId);
        Assert.Equal("Updated Station EN", station.Name.Translate("en"));
        Assert.Equal("Uuendatud jaam ET", station.Name.Translate("et"));
        Assert.Equal(EStationStatus.InUse, station.Status);
        Assert.True(chargingDb.ChargingStationConnectors.Any(link => link.ChargingStationId == stationId && link.ConnectorId == connectorB));
        Assert.False(chargingDb.ChargingStationConnectors.Any(link => link.ChargingStationId == stationId && link.ConnectorId == connectorA));
    }

    [Fact]
    public async Task Station_Details_ForeignCompany_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var ownCompanyId = Guid.NewGuid();
        var foreignCompanyId = Guid.NewGuid();
        var foreignStationId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, ownCompanyId, connectorId);
        await SeedForeignStation(authFactory, foreignCompanyId, foreignStationId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var response = await client.GetAsync($"/Company/Station/Details/{foreignStationId}?companyId={foreignCompanyId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Station_Delete_ForeignCompany_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var ownCompanyId = Guid.NewGuid();
        var foreignCompanyId = Guid.NewGuid();
        var foreignStationId = Guid.NewGuid();
        var ownStationId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, ownCompanyId, connectorId, stationId: ownStationId);
        await SeedForeignStation(authFactory, foreignCompanyId, foreignStationId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var index = await client.GetAsync($"/Company/Station/Index?companyId={ownCompanyId}");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        var indexDoc = await HtmlHelpers.GetDocumentAsync(index);
        var deleteForm = Assert.IsAssignableFrom<IHtmlFormElement>(indexDoc.QuerySelector("form[action*='Delete']"));
        var deleteSubmit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(deleteForm.QuerySelectorAll("button[type=submit]")));
        var post = await client.SendAsync(deleteForm, deleteSubmit, new Dictionary<string, string>
        {
            ["companyId"] = foreignCompanyId.ToString(),
            ["id"] = foreignStationId.ToString()
        });

        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var chargingDb = scope.ServiceProvider.GetRequiredService<ChargingDbContext>();
        Assert.True(chargingDb.ChargingStations.Any(s => s.Id == foreignStationId));
    }

    [Fact]
    public async Task Station_Index_ShowsOnlyCompanyStations()
    {
        var userId = Guid.NewGuid();
        var ownCompanyId = Guid.NewGuid();
        var foreignCompanyId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        var ownStationId = Guid.NewGuid();
        var foreignStationId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyOwnerData(authFactory, userId, ownCompanyId, connectorId, stationId: ownStationId);
        await SeedForeignStation(authFactory, foreignCompanyId, foreignStationId);

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var response = await client.GetAsync($"/Company/Station/Index?companyId={ownCompanyId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Existing Station", html);
        Assert.DoesNotContain("Foreign Station", html);
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

    private static async Task SeedCompanyOwnerData(
        WebApplicationFactory<Program> factory,
        Guid userId,
        Guid companyId,
        Guid connectorA,
        Guid? connectorB = null,
        Guid? stationId = null)
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

        if (!chargingDb.Connectors.Any(c => c.Id == connectorA))
        {
            chargingDb.Connectors.Add(new Connector { Id = connectorA, Name = new LangStr("CCS"), IsActive = true });
        }

        if (connectorB.HasValue && !chargingDb.Connectors.Any(c => c.Id == connectorB.Value))
        {
            chargingDb.Connectors.Add(new Connector { Id = connectorB.Value, Name = new LangStr("Type 2"), IsActive = true });
        }

        if (stationId.HasValue && !chargingDb.ChargingStations.Any(s => s.Id == stationId.Value))
        {
            chargingDb.ChargingStations.Add(new ChargingStation
            {
                Id = stationId.Value,
                Name = new LangStr("Existing Station"),
                Location = "Tallinn",
                Status = EStationStatus.Available,
                PricePerKwh = 0.35m,
                MaxPower = 150m,
                IsActive = true,
                CompanyId = companyId
            });

            chargingDb.ChargingStationConnectors.Add(new ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = stationId.Value,
                ConnectorId = connectorA
            });
        }

        await usersDb.SaveChangesAsync();
        await companiesDb.SaveChangesAsync();
        await chargingDb.SaveChangesAsync();
    }

    private static async Task SeedForeignStation(WebApplicationFactory<Program> factory, Guid companyId, Guid stationId)
    {
        using var scope = factory.Services.CreateScope();
        var companiesDb = scope.ServiceProvider.GetRequiredService<CompaniesDbContext>();
        var chargingDb = scope.ServiceProvider.GetRequiredService<ChargingDbContext>();

        if (!companiesDb.Companies.Any(c => c.Id == companyId))
        {
            companiesDb.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Foreign",
                ContactEmail = "foreign@test.local",
                ContactPhone = "+3727000000",
                Slug = $"foreign-{companyId:N}",
                IsActive = true
            });
        }

        if (!chargingDb.ChargingStations.Any(s => s.Id == stationId))
        {
            chargingDb.ChargingStations.Add(new ChargingStation
            {
                Id = stationId,
                Name = new LangStr("Foreign Station"),
                Location = "Parnu",
                Status = EStationStatus.Available,
                PricePerKwh = 0.33m,
                MaxPower = 90m,
                IsActive = true,
                CompanyId = companyId
            });
        }

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

        var action = form.Action;
        return client.PostAsync(action, new FormUrlEncodedContent(values));
    }
}
