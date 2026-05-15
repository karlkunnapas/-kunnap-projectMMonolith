using App.DAL.EF;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;

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
        var usersModuleApi = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();
        var companiesModuleApi = scope.ServiceProvider.GetRequiredService<ICompaniesModuleApi>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        await EnsureRoleExistsAsync(roleManager, "Customer");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"company.owner.{suffix}@example.com";
        var slug = $"tenant-{suffix}";

        var userRegistration = await usersModuleApi.RegisterCustomerAsync(new RegisterCustomerContract
        {
            FirstName = "Company",
            LastName = "Owner",
            Email = email,
            PhoneNumber = "+37255550000",
            Password = "Test.123"
        });

        Assert.True(userRegistration.Success);
        var result = await companiesModuleApi.CreateCompanyWithOwnerMembershipAsync(new CreateCompanyWithOwnerMembershipContract
        {
            OwnerUserId = userRegistration.UserId,
            ContactEmail = email,
            ContactPhone = "+37255550000",
            CompanyName = $"Company {suffix}",
            CompanySlug = slug
        });
        Assert.True(result.Success);
        var companyId = result.CompanyId;
        Assert.NotEqual(Guid.Empty, companyId);

        var company = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == companyId);
        Assert.NotNull(company);
        Assert.Equal(slug, company!.Slug);

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

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
        var usersModuleApi = scope.ServiceProvider.GetRequiredService<IUsersModuleApi>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        await EnsureRoleExistsAsync(roleManager, "Customer");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"customer.{suffix}@example.com";

        var result = await usersModuleApi.RegisterCustomerAsync(new RegisterCustomerContract
        {
            FirstName = "Regular",
            LastName = "Customer",
            Email = email,
            PhoneNumber = "+37255551111",
            Password = "Test.123"
        });

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




