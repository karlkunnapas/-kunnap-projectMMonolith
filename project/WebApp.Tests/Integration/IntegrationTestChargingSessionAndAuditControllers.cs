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
    public async Task CompanyAudit_TenantSlug_DeniesForeignCompany()
    {
        var userId = Guid.NewGuid();

        await using var authFactory = CreateAuthenticatedFactory();
        Guid companyA;
        Guid companyB;
        Guid foreignCompany;
        string companyASlug;
        string foreignCompanySlug;

        using (var scope = authFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            companyA = Guid.NewGuid();
            companyB = Guid.NewGuid();
            foreignCompany = Guid.NewGuid();
            companyASlug = $"a-{Guid.NewGuid():N}";
            foreignCompanySlug = $"c-{Guid.NewGuid():N}";

            db.Users.Add(new AppUser { Id = userId, UserName = $"owner-{userId}", Email = $"owner-{userId}@test.local" });
            db.Companies.AddRange(
                new Company { Id = companyA, Name = "A", ContactEmail = "a@test.local", ContactPhone = "+3723000000", Slug = companyASlug, IsActive = true },
                new Company { Id = companyB, Name = "B", ContactEmail = "b@test.local", ContactPhone = "+3723000001", Slug = $"b-{Guid.NewGuid():N}", IsActive = true },
                new Company { Id = foreignCompany, Name = "C", ContactEmail = "c@test.local", ContactPhone = "+3723000002", Slug = foreignCompanySlug, IsActive = true }
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

        var denied = await client.GetAsync($"/{foreignCompanySlug}/Company/Audit/CompanyLog");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var allowed = await client.GetAsync($"/{companyASlug}/Company/Audit/CompanyLog");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task CompanySidebar_AuditLink_PreservesCurrentCompanySlug()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var companySlug = $"audit-link-{Guid.NewGuid():N}";

        await using var authFactory = CreateAuthenticatedFactory();
        using (var scope = authFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Users.Add(new AppUser { Id = userId, UserName = $"owner-{userId}", Email = $"owner-{userId}@test.local" });
            db.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Audit Link Company",
                ContactEmail = "audit-link@test.local",
                ContactPhone = "+3725000000",
                Slug = companySlug,
                IsActive = true
            });
            db.AppUserCompanies.Add(new AppUserCompany
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                CompanyId = companyId,
                Role = ECompanyRole.Owner,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var response = await client.GetAsync($"/{companySlug}/Company/Dashboard/Index");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await HtmlHelpers.GetDocumentAsync(response);
        var auditLink = document.QuerySelectorAll("a")
            .OfType<IHtmlAnchorElement>()
            .FirstOrDefault(link => link.PathName.EndsWith("/Company/Audit/CompanyLog", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(auditLink);
        Assert.Contains($"/{companySlug}/Company/Audit/CompanyLog", auditLink!.Href, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("companyId=", auditLink.Href, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompanyFindStations_KeepsTenantSlug_AndAuditLinkStillTenantScoped()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var companySlug = $"find-stations-{Guid.NewGuid():N}";
        var stationId = Guid.NewGuid();

        await using var authFactory = CreateAuthenticatedFactory();
        using (var scope = authFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Users.Add(new AppUser { Id = userId, UserName = $"owner-{userId}", Email = $"owner-{userId}@test.local" });
            db.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Find Stations Company",
                ContactEmail = "find-stations@test.local",
                ContactPhone = "+3725100000",
                Slug = companySlug,
                IsActive = true
            });
            db.AppUserCompanies.Add(new AppUserCompany
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                CompanyId = companyId,
                Role = ECompanyRole.Owner,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            });
            db.ChargingStations.Add(new ChargingStation
            {
                Id = stationId,
                CompanyId = companyId,
                Name = new LangStr { ["en"] = "Find Stations Test Station" },
                Location = "Tallinn",
                Status = EStationStatus.Available,
                PricePerKwh = 0.30m,
                MaxPower = 50m,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var dashboardResponse = await client.GetAsync($"/{companySlug}/Company/Dashboard/Index");
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);

        var dashboardDocument = await HtmlHelpers.GetDocumentAsync(dashboardResponse);
        var findStationsLink = dashboardDocument.QuerySelectorAll("a")
            .OfType<IHtmlAnchorElement>()
            .FirstOrDefault(link =>
                link.PathName.Equals($"/{companySlug}", StringComparison.OrdinalIgnoreCase)
                || link.PathName.EndsWith("/Home/Index", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(findStationsLink);
        Assert.Contains($"/{companySlug}", findStationsLink!.Href, StringComparison.OrdinalIgnoreCase);

        var findStationsResponse = await client.GetAsync(findStationsLink.PathName);
        Assert.Equal(HttpStatusCode.OK, findStationsResponse.StatusCode);

        var findStationsDocument = await HtmlHelpers.GetDocumentAsync(findStationsResponse);
        var auditLink = findStationsDocument.QuerySelectorAll("a")
            .OfType<IHtmlAnchorElement>()
            .FirstOrDefault(link => link.PathName.EndsWith("/Company/Audit/CompanyLog", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(auditLink);
        Assert.Contains($"/{companySlug}/Company/Audit/CompanyLog", auditLink!.Href, StringComparison.OrdinalIgnoreCase);

        var stationDetailsLink = findStationsDocument.QuerySelectorAll("a.station-button")
            .OfType<IHtmlAnchorElement>()
            .FirstOrDefault();

        Assert.NotNull(stationDetailsLink);
        Assert.Contains($"/{companySlug}/", stationDetailsLink!.Href, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/Station/", stationDetailsLink.Href, StringComparison.OrdinalIgnoreCase);

        var stationDetailsResponse = await client.GetAsync(stationDetailsLink.PathName);
        Assert.Equal(HttpStatusCode.OK, stationDetailsResponse.StatusCode);

        var stationDetailsDocument = await HtmlHelpers.GetDocumentAsync(stationDetailsResponse);
        var stationDetailsAuditLink = stationDetailsDocument.QuerySelectorAll("a")
            .OfType<IHtmlAnchorElement>()
            .FirstOrDefault(link => link.PathName.EndsWith("/Company/Audit/CompanyLog", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(stationDetailsAuditLink);
        Assert.Contains($"/{companySlug}/Company/Audit/CompanyLog", stationDetailsAuditLink!.Href, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompanyAuditLog_ShowsMaintenanceMutations_ForCompany()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var companySlug = $"maintenance-audit-{Guid.NewGuid():N}";
        var stationId = Guid.NewGuid();

        await using var authFactory = CreateAuthenticatedFactory();
        using (var scope = authFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Users.Add(new AppUser { Id = userId, UserName = $"owner-{userId}", Email = $"owner-{userId}@test.local" });
            db.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Maintenance Audit Company",
                ContactEmail = "maintenance-audit@test.local",
                ContactPhone = "+3726000000",
                Slug = companySlug,
                IsActive = true
            });
            db.AppUserCompanies.Add(new AppUserCompany
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                CompanyId = companyId,
                Role = ECompanyRole.Owner,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            });
            db.ChargingStations.Add(new ChargingStation
            {
                Id = stationId,
                CompanyId = companyId,
                Name = new LangStr { ["en"] = "Maintenance Audit Station" },
                Location = "Tallinn",
                Status = EStationStatus.Available,
                PricePerKwh = 0.35m,
                MaxPower = 80m,
                IsActive = true
            });
            await db.SaveChangesAsync();

            db.Maintenances.Add(new Maintenance
            {
                Id = Guid.NewGuid(),
                ChargingStationId = stationId,
                ReportedByUserId = userId,
                IssueDescription = "Connector overheating",
                Status = EMaintenanceStatus.Reported,
                ReportedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var response = await client.GetAsync($"/{companySlug}/Company/Audit/CompanyLog?entityName=Maintenance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("No audit entries found.", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Maintenance", html, StringComparison.OrdinalIgnoreCase);
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
