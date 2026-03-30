using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApp.Tests.Integration;

[Collection("Database tests")]
public class IntegrationTestIdentityRegistrationMembership : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public IntegrationTestIdentityRegistrationMembership(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterCompanyOwner_CreatesCompanyMembership_AndOwnerRole()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        await EnsureRoleExistsAsync(roleManager, "CompanyOwner");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"company.owner.{suffix}@example.com";
        var slug = $"tenant-{suffix}";

        var dto = new RegisterCompanyOwnerDto
        {
            FirstName = "Company",
            LastName = "Owner",
            Email = email,
            PhoneNumber = "+37255550000",
            Password = "Test.123",
            ConfirmPassword = "Test.123",
            CompanyName = $"Company {suffix}",
            CompanySlug = slug
        };

        var result = await identityService.RegisterCompanyOwnerAsync(dto);

        Assert.True(result.Success);
        var companyId = result.Data;
        Assert.NotEqual(Guid.Empty, companyId);

        var company = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == companyId);
        Assert.NotNull(company);
        Assert.Equal(slug, company!.Slug);

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var isOwnerRole = await userManager.IsInRoleAsync(user, "CompanyOwner");
        Assert.True(isOwnerRole);

        var membership = await db.AppUserCompanies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.AppUserId == user.Id && x.CompanyId == companyId);

        Assert.NotNull(membership);
        Assert.Equal(ECompanyRole.Owner, membership!.Role);
        Assert.True(membership.IsActive);
    }

    [Fact]
    public async Task RegisterCustomer_CreatesUserWithoutCompanyMembership()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        await EnsureRoleExistsAsync(roleManager, "Customer");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"customer.{suffix}@example.com";

        var dto = new RegisterCustomerDto
        {
            FirstName = "Regular",
            LastName = "Customer",
            Email = email,
            PhoneNumber = "+37255551111",
            Password = "Test.123",
            ConfirmPassword = "Test.123"
        };

        var result = await identityService.RegisterCustomerAsync(dto);

        Assert.True(result.Success);

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var isCustomerRole = await userManager.IsInRoleAsync(user, "Customer");
        Assert.True(isCustomerRole);

        var memberships = await db.AppUserCompanies
            .IgnoreQueryFilters()
            .Where(x => x.AppUserId == user.Id)
            .ToListAsync();

        Assert.Empty(memberships);
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





