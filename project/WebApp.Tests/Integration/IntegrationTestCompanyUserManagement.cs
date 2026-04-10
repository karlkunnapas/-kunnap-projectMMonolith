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
public class IntegrationTestCompanyUserManagement : IClassFixture<CustomWebApplicationFactory<Program>>
{
    [Fact]
    public async Task CompanyUsers_Index_OwnerMembership_ReturnsOk()
    {
        var ownerUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyMembership(authFactory, ownerUserId, companyId, ECompanyRole.Owner, "owner@test.local");

        var client = CreateAuthenticatedClient(authFactory, ownerUserId, "CompanyOwner");
        var response = await client.GetAsync($"/Company/CompanyUsers/Index?companyId={companyId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CompanyUsers_Index_NonOwnerMembership_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyMembership(authFactory, userId, companyId, ECompanyRole.Employee, "employee@test.local");

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var response = await client.GetAsync($"/Company/CompanyUsers/Index?companyId={companyId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CompanyUsers_Add_Post_ExistingIdentityUser_CreatesMembershipAndAudit()
    {
        var ownerUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyMembership(authFactory, ownerUserId, companyId, ECompanyRole.Owner, "owner@test.local");
        await SeedUser(authFactory, targetUserId, "existing-user@test.local");

        var client = CreateAuthenticatedClient(authFactory, ownerUserId, "CompanyOwner");
        var get = await client.GetAsync($"/Company/CompanyUsers/Add?companyId={companyId}");
        var document = await HtmlHelpers.GetDocumentAsync(get);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(document.QuerySelector("form"));
        var submit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(form.QuerySelectorAll("button[type=submit]")));

        var post = await client.SendAsync(form, submit, new Dictionary<string, string>
        {
            ["CompanyId"] = companyId.ToString(),
            ["Email"] = "existing-user@test.local",
            ["Role"] = ((int)ECompanyRole.Manager).ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.AppUserCompanies.Any(uc =>
            uc.CompanyId == companyId &&
            uc.AppUserId == targetUserId &&
            uc.Role == ECompanyRole.Manager &&
            uc.IsActive));
        Assert.True(db.AuditLogs.Any(log =>
            log.CompanyId == companyId &&
            log.Action == "ExistingUserLinked" &&
            log.EntityName == nameof(AppUserCompany)));
    }

    [Fact]
    public async Task CompanyUsers_Edit_Post_UpdatesMembershipRole()
    {
        var ownerUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyMembership(authFactory, ownerUserId, companyId, ECompanyRole.Owner, "owner@test.local");
        await SeedUser(authFactory, targetUserId, "member@test.local");
        var membershipId = await SeedMembership(authFactory, targetUserId, companyId, ECompanyRole.Employee);

        var client = CreateAuthenticatedClient(authFactory, ownerUserId, "CompanyOwner");
        var get = await client.GetAsync($"/Company/CompanyUsers/Edit?membershipId={membershipId}&companyId={companyId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var document = await HtmlHelpers.GetDocumentAsync(get);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(document.QuerySelector("form"));
        var submit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(form.QuerySelectorAll("button[type=submit]")));

        var post = await client.SendAsync(form, submit, new Dictionary<string, string>
        {
            ["CompanyId"] = companyId.ToString(),
            ["MembershipId"] = membershipId.ToString(),
            ["UserId"] = targetUserId.ToString(),
            ["Email"] = "member@test.local",
            ["Role"] = ((int)ECompanyRole.Manager).ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(ECompanyRole.Manager, db.AppUserCompanies.Single(uc => uc.Id == membershipId).Role);
    }

    [Fact]
    public async Task CompanyUsers_Remove_Post_LastOwner_IsBlocked()
    {
        var ownerUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyMembership(authFactory, ownerUserId, companyId, ECompanyRole.Owner, "owner@test.local");
        var ownerMembershipId = await GetMembershipId(authFactory, ownerUserId, companyId);

        var client = CreateAuthenticatedClient(authFactory, ownerUserId, "CompanyOwner");
        var indexGet = await client.GetAsync($"/Company/CompanyUsers/Index?companyId={companyId}");
        var indexDoc = await HtmlHelpers.GetDocumentAsync(indexGet);
        var removeForm = Assert.IsAssignableFrom<IHtmlFormElement>(Assert.Single(indexDoc.QuerySelectorAll("form[action*='Remove']")));
        var submit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(removeForm.QuerySelectorAll("button[type=submit]")));

        var post = await client.SendAsync(removeForm, submit, new Dictionary<string, string>
        {
            ["companyId"] = companyId.ToString(),
            ["membershipId"] = ownerMembershipId.ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using var scope = authFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.AppUserCompanies.Single(uc => uc.Id == ownerMembershipId).IsActive);
    }

    [Fact]
    public async Task CompanyUsers_Remove_Post_Employee_IsDeactivated_AndHiddenFromIndex()
    {
        var ownerUserId = Guid.NewGuid();
        var employeeUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyMembership(authFactory, ownerUserId, companyId, ECompanyRole.Owner, "owner@test.local");
        await SeedUser(authFactory, employeeUserId, "employee-remove@test.local");
        var employeeMembershipId = await SeedMembership(authFactory, employeeUserId, companyId, ECompanyRole.Employee);

        var client = CreateAuthenticatedClient(authFactory, ownerUserId, "CompanyOwner");
        var indexGet = await client.GetAsync($"/Company/CompanyUsers/Index?companyId={companyId}");
        var indexDoc = await HtmlHelpers.GetDocumentAsync(indexGet);
        var removeForm = Assert.IsAssignableFrom<IHtmlFormElement>(
            Assert.Single(indexDoc.QuerySelectorAll($"form input[name='membershipId'][value='{employeeMembershipId}']").Select(i => i.ParentElement)));
        var submit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(removeForm.QuerySelectorAll("button[type=submit]")));

        var post = await client.SendAsync(removeForm, submit, new Dictionary<string, string>
        {
            ["companyId"] = companyId.ToString(),
            ["membershipId"] = employeeMembershipId.ToString()
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        using (var scope = authFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(db.AppUserCompanies.Single(uc => uc.Id == employeeMembershipId).IsActive);
        }

        var indexAfter = await client.GetAsync($"/Company/CompanyUsers/Index?companyId={companyId}");
        var htmlAfter = await indexAfter.Content.ReadAsStringAsync();
        Assert.DoesNotContain("employee-remove@test.local", htmlAfter, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Account_CompanySelection_Post_ValidMembership_RedirectsToSelectedCompanyAndLogsSwitch()
    {
        var userId = Guid.NewGuid();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyMembership(authFactory, userId, companyA, ECompanyRole.Owner, "owner@test.local", "alpha-company");
        await SeedCompanyMembership(authFactory, userId, companyB, ECompanyRole.Owner, "owner@test.local", "beta-company");

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var get = await client.GetAsync("/Account/CompanySelection");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var document = await HtmlHelpers.GetDocumentAsync(get);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(document.QuerySelector("form"));
        var submit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(form.QuerySelectorAll("button[type=submit]")));
        var post = await client.SendAsync(form, submit, new Dictionary<string, string>
        {
            ["SelectedCompanyId"] = companyB.ToString(),
            ["ReturnUrl"] = ""
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Contains("beta-company", post.Headers.Location?.ToString() ?? string.Empty);

        using var scope = authFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.AuditLogs.Any(log =>
            log.CompanyId == companyB &&
            log.Action == "CompanySwitched" &&
            log.EntityName == nameof(AppUserCompany)));
    }

    [Fact]
    public async Task Account_CompanySelection_Post_WithTenantReturnUrl_RewritesSlugToSelectedCompany()
    {
        var userId = Guid.NewGuid();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyMembership(authFactory, userId, companyA, ECompanyRole.Owner, "owner@test.local", "alpha-company");
        await SeedCompanyMembership(authFactory, userId, companyB, ECompanyRole.Owner, "owner@test.local", "beta-company");

        var client = CreateAuthenticatedClient(authFactory, userId, "CompanyOwner");
        var get = await client.GetAsync("/Account/CompanySelection?returnUrl=%2Falpha-company%2FCompany%2FPromotion%2FIndex%3FcompanyId%3D00000000-0000-0000-0000-000000000001");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var document = await HtmlHelpers.GetDocumentAsync(get);
        var form = Assert.IsAssignableFrom<IHtmlFormElement>(document.QuerySelector("form"));
        var submit = Assert.IsAssignableFrom<IHtmlElement>(Assert.Single(form.QuerySelectorAll("button[type=submit]")));
        var post = await client.SendAsync(form, submit, new Dictionary<string, string>
        {
            ["SelectedCompanyId"] = companyB.ToString(),
            ["ReturnUrl"] = "/alpha-company/Company/Promotion/Index?companyId=00000000-0000-0000-0000-000000000001"
        });

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        var location = post.Headers.Location?.ToString() ?? string.Empty;
        Assert.Contains("/beta-company/Company/Promotion/Index", location, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/alpha-company/Company/Promotion/Index", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Employee_MaintenanceAccessible_ButDashboardForbidden()
    {
        var employeeUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyMembership(authFactory, employeeUserId, companyId, ECompanyRole.Employee, "employee@test.local");

        var client = CreateAuthenticatedClient(authFactory, employeeUserId);
        var maintenance = await client.GetAsync($"/Company/Maintenance/Index?companyId={companyId}");
        var dashboard = await client.GetAsync($"/Company/Dashboard/Index?companyId={companyId}");

        Assert.Equal(HttpStatusCode.OK, maintenance.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, dashboard.StatusCode);
    }

    [Fact]
    public async Task Employee_MaintenanceIndex_ShowsIssues_AndHidesDashboardLink()
    {
        var employeeUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyMembership(authFactory, employeeUserId, companyId, ECompanyRole.Employee, "employee@test.local");
        await SeedMaintenanceIssue(authFactory, companyId, "Employee visible issue");

        var client = CreateAuthenticatedClient(authFactory, employeeUserId);
        var maintenance = await client.GetAsync($"/Company/Maintenance/Index?companyId={companyId}");
        var html = await maintenance.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, maintenance.StatusCode);
        Assert.Contains("Employee visible issue", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Back to dashboard", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manager_CanAccessDashboard_ButCannotManageUsers()
    {
        var managerUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        await using var authFactory = CreateAuthenticatedFactory();
        await SeedCompanyMembership(authFactory, managerUserId, companyId, ECompanyRole.Manager, "manager@test.local");

        var client = CreateAuthenticatedClient(authFactory, managerUserId);
        var dashboard = await client.GetAsync($"/Company/Dashboard/Index?companyId={companyId}");
        var users = await client.GetAsync($"/Company/CompanyUsers/Index?companyId={companyId}");

        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);
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

    private static async Task SeedCompanyMembership(
        WebApplicationFactory<Program> factory,
        Guid userId,
        Guid companyId,
        ECompanyRole role,
        string email,
        string? slug = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!db.Users.Any(u => u.Id == userId))
        {
            db.Users.Add(new AppUser
            {
                Id = userId,
                UserName = email,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true
            });
        }

        if (!db.Companies.Any(c => c.Id == companyId))
        {
            db.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Company",
                ContactEmail = "company@test.local",
                ContactPhone = "+3725000000",
                Slug = slug ?? $"company-{companyId:N}",
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
                Role = role,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
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

    private static async Task<Guid> SeedMembership(WebApplicationFactory<Program> factory, Guid userId, Guid companyId, ECompanyRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = db.AppUserCompanies.FirstOrDefault(uc => uc.AppUserId == userId && uc.CompanyId == companyId);
        if (existing != null)
        {
            return existing.Id;
        }

        var membership = new AppUserCompany
        {
            Id = Guid.NewGuid(),
            AppUserId = userId,
            CompanyId = companyId,
            Role = role,
            IsActive = true,
            JoinedAtUtc = DateTime.UtcNow
        };
        db.AppUserCompanies.Add(membership);
        await db.SaveChangesAsync();
        return membership.Id;
    }

    private static async Task<Guid> GetMembershipId(WebApplicationFactory<Program> factory, Guid userId, Guid companyId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = await db.AppUserCompanies
            .AsNoTracking()
            .FirstAsync(uc => uc.AppUserId == userId && uc.CompanyId == companyId);
        return membership.Id;
    }

    private static async Task SeedMaintenanceIssue(WebApplicationFactory<Program> factory, Guid companyId, string issueDescription)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = "Seed Station",
            Location = "Seed Location",
            Status = EStationStatus.Maintenance,
            PricePerKwh = 0.30m,
            MaxPower = 50,
            IsActive = true,
            CompanyId = companyId
        };
        db.ChargingStations.Add(station);

        db.Maintenances.Add(new Maintenance
        {
            Id = Guid.NewGuid(),
            ChargingStationId = station.Id,
            IssueDescription = issueDescription,
            Status = EMaintenanceStatus.Reported,
            ReportedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
