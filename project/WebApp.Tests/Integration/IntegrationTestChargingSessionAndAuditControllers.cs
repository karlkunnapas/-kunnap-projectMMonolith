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
public class IntegrationTestChargingSessionAndAuditControllers : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _anonymousClient;

    public IntegrationTestChargingSessionAndAuditControllers(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Get_ChargingSessionHistory_AnonymousUser_IsRedirectedToLogin()
    {
        var response = await _anonymousClient.GetAsync("/Root/ChargingSession/History");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_AdminAuditTrail_AnonymousUser_IsRedirectedToLogin()
    {
        var response = await _anonymousClient.GetAsync($"/Admin/Audit/Trail?entityName=Company&entityId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_CompanyAuditLog_AnonymousUser_IsRedirectedToLogin()
    {
        var response = await _anonymousClient.GetAsync("/Company/Audit/CompanyLog");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SessionStartAndStop_AuthenticatedCustomer_SucceedsAndPersistsCalculatedResults()
    {
        var userId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        using (var scope = authFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Session Company",
                ContactEmail = "session@test.local",
                ContactPhone = "+3721000000",
                Slug = $"session-{Guid.NewGuid():N}",
                IsActive = true
            };
            var station = new ChargingStation
            {
                Id = Guid.NewGuid(),
                Name = new LangStr { ["en"] = "Integration Station" },
                Location = "Tallinn",
                Status = EStationStatus.Available,
                PricePerKwh = 0.40m,
                MaxPower = 120m,
                IsActive = true,
                CompanyId = company.Id
            };
            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ChargingStationId = station.Id,
                StartTime = DateTime.UtcNow.AddMinutes(-2),
                EndTime = DateTime.UtcNow.AddMinutes(28),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(13),
                Status = EReservationStatus.Active,
                EstimatedCost = 12m
            };

            db.Companies.Add(company);
            db.ChargingStations.Add(station);
            db.Reservations.Add(reservation);
            db.Users.Add(new AppUser { Id = userId, UserName = $"customer-{userId}", Email = $"customer-{userId}@test.local" });
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(authFactory, userId, "Customer");

        var getStart = await client.GetAsync($"/Root/ChargingSession/Start?reservationId={GetReservationId(authFactory, userId)}");
        Assert.Equal(HttpStatusCode.OK, getStart.StatusCode);

        var startDoc = await HtmlHelpers.GetDocumentAsync(getStart);
        var startForm = Assert.IsAssignableFrom<IHtmlFormElement>(startDoc.QuerySelector("form"));
        var startSubmit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(startForm.QuerySelectorAll("button[type=submit]")));
        var postStart = await client.SendAsync(startForm, startSubmit);

        Assert.Equal(HttpStatusCode.Redirect, postStart.StatusCode);
        Assert.Contains("/Root/ChargingSession/Details", postStart.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var sessionId = GetSessionId(authFactory, userId);
        var getDetails = await client.GetAsync($"/Root/ChargingSession/Details/{sessionId}");
        Assert.Equal(HttpStatusCode.OK, getDetails.StatusCode);

        var detailsDoc = await HtmlHelpers.GetDocumentAsync(getDetails);
        var stopForm = Assert.IsAssignableFrom<IHtmlFormElement>(detailsDoc.QuerySelector("form"));
        var stopSubmit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(stopForm.QuerySelectorAll("button[type=submit]")));
        var postStop = await client.SendAsync(stopForm, stopSubmit);

        Assert.Equal(HttpStatusCode.Redirect, postStop.StatusCode);

        using var verifyScope = authFactory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = verifyDb.ChargingSessions.Single(s => s.Id == sessionId);
        Assert.NotNull(session.EndTime);
        Assert.True(session.EnergyConsumed > 0m);
        Assert.True(session.Cost > 0m);
    }

    [Fact]
    public async Task SessionDetails_ForeignUser_ReturnsExplicitForbidden()
    {
        var ownerUserId = Guid.NewGuid();
        var foreignUserId = Guid.NewGuid();

        await using var authFactory = CreateAuthenticatedFactory();
        using (var scope = authFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Idor Company",
                ContactEmail = "idor@test.local",
                ContactPhone = "+3722000000",
                Slug = $"idor-{Guid.NewGuid():N}",
                IsActive = true
            };
            var station = new ChargingStation
            {
                Id = Guid.NewGuid(),
                Name = new LangStr { ["en"] = "IDOR Station" },
                Location = "Tartu",
                Status = EStationStatus.InUse,
                PricePerKwh = 0.45m,
                MaxPower = 100m,
                IsActive = true,
                CompanyId = company.Id
            };
            var session = new ChargingSession
            {
                Id = Guid.NewGuid(),
                UserId = ownerUserId,
                ChargingStationId = station.Id,
                StartTime = DateTime.UtcNow.AddMinutes(-10),
                EnergyConsumed = 0m,
                Cost = 0m
            };

            db.Companies.Add(company);
            db.ChargingStations.Add(station);
            db.ChargingSessions.Add(session);
            db.Users.Add(new AppUser { Id = ownerUserId, UserName = $"owner-{ownerUserId}", Email = $"owner-{ownerUserId}@test.local" });
            db.Users.Add(new AppUser { Id = foreignUserId, UserName = $"foreign-{foreignUserId}", Email = $"foreign-{foreignUserId}@test.local" });
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(authFactory, foreignUserId, "Customer");
        var sessionId = GetAnySessionId(authFactory, ownerUserId);

        var response = await client.GetAsync($"/Root/ChargingSession/Details/{sessionId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CompanyAudit_WithMultipleMemberships_DeniesForeignCompany()
    {
        var userId = Guid.NewGuid();

        await using var authFactory = CreateAuthenticatedFactory();
        Guid companyA;
        Guid companyB;
        Guid foreignCompany;

        using (var scope = authFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            companyA = Guid.NewGuid();
            companyB = Guid.NewGuid();
            foreignCompany = Guid.NewGuid();

            db.Users.Add(new AppUser { Id = userId, UserName = $"owner-{userId}", Email = $"owner-{userId}@test.local" });
            db.Companies.AddRange(
                new Company { Id = companyA, Name = "A", ContactEmail = "a@test.local", ContactPhone = "+3723000000", Slug = $"a-{Guid.NewGuid():N}", IsActive = true },
                new Company { Id = companyB, Name = "B", ContactEmail = "b@test.local", ContactPhone = "+3723000001", Slug = $"b-{Guid.NewGuid():N}", IsActive = true },
                new Company { Id = foreignCompany, Name = "C", ContactEmail = "c@test.local", ContactPhone = "+3723000002", Slug = $"c-{Guid.NewGuid():N}", IsActive = true }
            );

            db.AppUserCompanies.AddRange(
                new AppUserCompany { Id = Guid.NewGuid(), AppUserId = userId, CompanyId = companyA, Role = ECompanyRole.Owner, IsActive = true, JoinedAtUtc = DateTime.UtcNow.AddMinutes(-10) },
                new AppUserCompany { Id = Guid.NewGuid(), AppUserId = userId, CompanyId = companyB, Role = ECompanyRole.Owner, IsActive = true, JoinedAtUtc = DateTime.UtcNow }
            );

            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = foreignCompany,
                EntityName = nameof(ChargingSession),
                EntityId = Guid.NewGuid(),
                Action = "Update",
                UserName = "system",
                AtUtc = DateTime.UtcNow,
                ChangesJson = "[]"
            });

            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");

        var denied = await client.GetAsync($"/Company/Audit/CompanyLog?companyId={foreignCompany}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var allowed = await client.GetAsync($"/Company/Audit/CompanyLog?companyId={companyA}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
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

    private static Guid GetReservationId(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Reservations.Where(r => r.UserId == userId).OrderByDescending(r => r.StartTime).Select(r => r.Id).First();
    }

    private static Guid GetSessionId(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.ChargingSessions.Where(s => s.UserId == userId).OrderByDescending(s => s.StartTime).Select(s => s.Id).First();
    }

    private static Guid GetAnySessionId(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.ChargingSessions.Where(s => s.UserId == userId).Select(s => s.Id).First();
    }
}

