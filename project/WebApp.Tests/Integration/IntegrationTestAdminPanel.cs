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

    [Fact]
    public async Task AdminPromotions_Index_ReturnsSystemPromotions()
    {
        await using var authFactory = CreateAuthenticatedFactory();
        var adminUserId = Guid.NewGuid();
        await SeedUser(authFactory, adminUserId, "admin@test.local");
        await SeedSystemPromotion(authFactory, "SYSADMIN", 15m);

        var client = CreateAuthenticatedClient(authFactory, adminUserId, "SystemAdmin");
        var response = await client.GetAsync("/Admin/Promotions/Index");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("SYSADMIN", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminPromotions_Index_AdminRole_ReturnsSystemPromotions()
    {
        await using var authFactory = CreateAuthenticatedFactory();
        var adminUserId = Guid.NewGuid();
        await SeedUser(authFactory, adminUserId, "admin@test.local");
        await SeedSystemPromotion(authFactory, "SYSADMIN2", 10m);

        var client = CreateAuthenticatedClient(authFactory, adminUserId, "Admin");
        var response = await client.GetAsync("/Admin/Promotions/Index");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("SYSADMIN2", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminPromotions_Index_ShowsCompanyPromotionsInSeparateSection()
    {
        await using var authFactory = CreateAuthenticatedFactory();
        var adminUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await SeedUser(authFactory, adminUserId, "admin@test.local");
        await SeedCompany(authFactory, companyId, "promo-company");
        await SeedCompanyPromotion(authFactory, companyId, "COMPANYPROMO", 12m);

        var client = CreateAuthenticatedClient(authFactory, adminUserId, "Admin");
        var response = await client.GetAsync("/Admin/Promotions/Index");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Company Promotions", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COMPANYPROMO", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Integration Company", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminPromotions_Index_DefaultsToNonExpired_AndCanShowExpired()
    {
        await using var authFactory = CreateAuthenticatedFactory();
        var adminUserId = Guid.NewGuid();
        await SeedUser(authFactory, adminUserId, "admin@test.local");
        await SeedSystemPromotion(authFactory, "CURRENTPROMO", 15m, true, DateTime.UtcNow.AddDays(7));
        await SeedSystemPromotion(authFactory, "EXPIREDPROMO", 5m, true, DateTime.UtcNow.AddDays(-1));

        var client = CreateAuthenticatedClient(authFactory, adminUserId, "Admin");

        var defaultResponse = await client.GetAsync("/Admin/Promotions/Index");
        var defaultHtml = await defaultResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        Assert.Contains("CURRENTPROMO", defaultHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EXPIREDPROMO", defaultHtml, StringComparison.OrdinalIgnoreCase);

        var showExpiredResponse = await client.GetAsync("/Admin/Promotions/Index?showExpired=true");
        var showExpiredHtml = await showExpiredResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, showExpiredResponse.StatusCode);
        Assert.Contains("CURRENTPROMO", showExpiredHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXPIREDPROMO", showExpiredHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminPromotions_Create_Post_CreatesNewPromotion()
    {
        await using var authFactory = CreateAuthenticatedFactory();
        var adminUserId = Guid.NewGuid();
        await SeedUser(authFactory, adminUserId, "admin@test.local");

        var client = CreateAuthenticatedClient(authFactory, adminUserId, "SystemAdmin");
        
        // Get the form to extract the anti-forgery token
        var getResponse = await client.GetAsync("/Admin/Promotions/Create");
        var document = await HtmlHelpers.GetDocumentAsync(getResponse);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(document.QuerySelector("form"));
        
        // Extract anti-forgery token
        var antiForgeryInput = form.QuerySelector("input[name='__RequestVerificationToken']") as IHtmlInputElement;
        var token = antiForgeryInput?.Value ?? "";
        
        var from = DateTime.UtcNow;
        var to = DateTime.UtcNow.AddDays(30);
        
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Code", "NEWPROMO"),
            new KeyValuePair<string, string>("DiscountValue", "20"),
            new KeyValuePair<string, string>("ValidFromUtc", from.ToString("yyyy-MM-ddTHH:mm")),
            new KeyValuePair<string, string>("ValidToUtc", to.ToString("yyyy-MM-ddTHH:mm")),
            new KeyValuePair<string, string>("IsActive", "true")
        });

        var post = await client.PostAsync("/Admin/Promotions/Create", content);

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains("/Admin/Promotions/Index", post.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task AdminConnectorTypes_Index_ReturnsConnectorTypeRows()
    {
        await using var authFactory = CreateAuthenticatedFactory();
        var adminUserId = Guid.NewGuid();
        await SeedUser(authFactory, adminUserId, "admin@test.local");
        await SeedConnectorType(authFactory, "CCS", true);

        var client = CreateAuthenticatedClient(authFactory, adminUserId, "Admin");
        var response = await client.GetAsync("/Admin/ConnectorTypes/Index");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("CCS", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminConnectorTypes_Create_Post_CreatesConnectorType()
    {
        await using var authFactory = CreateAuthenticatedFactory();
        var adminUserId = Guid.NewGuid();
        await SeedUser(authFactory, adminUserId, "admin@test.local");

        var client = CreateAuthenticatedClient(authFactory, adminUserId, "Admin");
        var getResponse = await client.GetAsync("/Admin/ConnectorTypes/Create");
        var document = await HtmlHelpers.GetDocumentAsync(getResponse);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(document.QuerySelector("form"));
        var antiForgeryInput = form.QuerySelector("input[name='__RequestVerificationToken']") as IHtmlInputElement;
        var token = antiForgeryInput?.Value ?? "";

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("NameEn", "NACS"),
            new KeyValuePair<string, string>("NameEt", "NACS ET"),
            new KeyValuePair<string, string>("IsActive", "true")
        });

        var post = await client.PostAsync("/Admin/ConnectorTypes/Create", content);

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains("/Admin/ConnectorTypes/Index", post.Headers.Location?.ToString() ?? "");
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

    private static async Task SeedSystemPromotion(WebApplicationFactory<Program> factory, string code, decimal discount, bool isActive = true, DateTime? validTo = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Promotions.Add(new Promotion
        {
            Id = Guid.NewGuid(),
            Code = code,
            DiscountValue = discount,
            ValidFrom = DateTime.UtcNow,
            ValidTo = validTo ?? DateTime.UtcNow.AddDays(30),
            IsActive = isActive,
            CompanyId = null
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCompanyPromotion(WebApplicationFactory<Program> factory, Guid companyId, string code, decimal discount)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Promotions.Add(new Promotion
        {
            Id = Guid.NewGuid(),
            Code = code,
            DiscountValue = discount,
            ValidFrom = DateTime.UtcNow,
            ValidTo = DateTime.UtcNow.AddDays(30),
            IsActive = true,
            CompanyId = companyId
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedConnectorType(WebApplicationFactory<Program> factory, string nameEn, bool isActive)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Connectors.Add(new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr(nameEn),
            IsActive = isActive
        });
        await db.SaveChangesAsync();
    }
}
