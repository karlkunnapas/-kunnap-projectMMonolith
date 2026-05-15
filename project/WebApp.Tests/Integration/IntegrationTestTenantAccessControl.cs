using System.Globalization;
using System.IO;
using System.Security.Claims;
using App.DAL.EF;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Companies;
using Shared.Contracts.Tenancy;
using Shared.Contracts.Users;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestTenantAccessControl : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public IntegrationTestTenantAccessControl(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CompanyUser_AccessingAnotherCompanyTenant_IsDenied()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var usersModuleApi = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var companiesModuleApi = scope.ServiceProvider.GetRequiredService<ICompaniesModuleApi>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        await EnsureRoleExistsAsync(roleManager, "CompanyOwner");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var ownerEmail = $"owner.{suffix}@example.com";
        var ownerCompanySlug = $"owner-{suffix}";
        var targetCompanySlug = $"target-{suffix}";

        var userRegistration = await usersModuleApi.RegisterCustomerAsync(new RegisterCustomerContract
        {
            FirstName = "Owner",
            LastName = "User",
            Email = ownerEmail,
            PhoneNumber = "+37255552222",
            Password = "Test.123"
        });
        Assert.True(userRegistration.Success);

        var registerResult = await companiesModuleApi.CreateCompanyWithOwnerMembershipAsync(new CreateCompanyWithOwnerMembershipContract
        {
            OwnerUserId = userRegistration.UserId,
            ContactEmail = ownerEmail,
            ContactPhone = "+37255552222",
            CompanyName = $"Owner Company {suffix}",
            CompanySlug = ownerCompanySlug
        });

        Assert.True(registerResult.Success);
        var ownerCompanyId = registerResult.CompanyId;
        Assert.NotEqual(Guid.Empty, ownerCompanyId);

        var ownerMembership = await db.AppUserCompanies
            .IgnoreQueryFilters()
            .Include(x => x.AppUser)
            .FirstAsync(x => x.CompanyId == ownerCompanyId);

        var targetCompany = new Company
        {
            Name = new LangStr($"Target Company {suffix}"),
            ContactEmail = $"target.{suffix}@example.com",
            ContactPhone = "+37255553333",
            Slug = targetCompanySlug,
            IsActive = true
        };

        db.Companies.Add(targetCompany);
        await db.SaveChangesAsync();

        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");

            var context = new DefaultHttpContext();
            context.Request.Path = $"/{targetCompanySlug}/festivaleditions";
            context.Response.Body = new MemoryStream();

            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, ownerMembership.AppUserId.ToString()),
                new Claim(ClaimTypes.Email, ownerEmail)
            }, "TestAuth"));

            var tenantContext = new TenantContext();
            var nextCalled = false;
            RequestDelegate next = _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new TenantResolutionMiddleware(next);
            await middleware.InvokeAsync(context, tenantContext, companiesModuleApi);

            Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
            Assert.False(nextCalled);
            Assert.False(tenantContext.IsResolved);

            context.Response.Body.Position = 0;
            using var reader = new StreamReader((Stream)context.Response.Body);
            var body = await reader.ReadToEndAsync();
            Assert.Equal("Access denied for this company.", body);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    private static async Task EnsureRoleExistsAsync(RoleManager<AppRole> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var roleResult = await roleManager.CreateAsync(new AppRole { Name = roleName });
        Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
    }
}

