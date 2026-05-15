using Microsoft.AspNetCore.Identity;
using Modules.Charging.Infrastructure;
using Modules.Charging.Infrastructure.Seeding;
using Modules.Companies.Infrastructure;
using Modules.Companies.Infrastructure.Seeding;
using Modules.Users.Domain;

namespace WebApp.Tests.Helpers;

internal static class DataSeeder
{
    internal static void SeedData(
        CompaniesDbContext companiesDbContext,
        ChargingDbContext chargingDbContext,
        UserManager<AppUser> userManager)
    {
        var owner = userManager.FindByEmailAsync(CompaniesModuleDataSeeder.SeedCompanyOwnerEmail)
            .GetAwaiter()
            .GetResult();

        if (owner == null)
        {
            owner = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = CompaniesModuleDataSeeder.SeedCompanyOwnerEmail,
                NormalizedUserName = CompaniesModuleDataSeeder.SeedCompanyOwnerEmail.ToUpperInvariant(),
                Email = CompaniesModuleDataSeeder.SeedCompanyOwnerEmail,
                NormalizedEmail = CompaniesModuleDataSeeder.SeedCompanyOwnerEmail.ToUpperInvariant(),
                EmailConfirmed = true,
                PhoneNumber = "+3725000000"
            };

            userManager.CreateAsync(owner, "Owner.123").GetAwaiter().GetResult();
            userManager.AddToRoleAsync(owner, "CompanyOwner").GetAwaiter().GetResult();
        }

        var companyId = CompaniesModuleDataSeeder
            .SeedCompanyAndOwnerMembershipAsync(companiesDbContext, owner.Id)
            .GetAwaiter()
            .GetResult();

        ChargingModuleDataSeeder
            .SeedDataAsync(chargingDbContext, companyId)
            .GetAwaiter()
            .GetResult();
    }
}
