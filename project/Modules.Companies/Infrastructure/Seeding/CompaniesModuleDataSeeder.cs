using Modules.Companies.Domain;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace Modules.Companies.Infrastructure.Seeding;

internal static class CompaniesModuleDataSeeder
{
    internal const string SeedCompanySlug = "seed-company";
    internal const string SeedCompanyOwnerEmail = "owner@seed.com";

    internal static async Task<Guid> SeedCompanyAndOwnerMembershipAsync(
        CompaniesDbContext context,
        Guid ownerUserId,
        CancellationToken ct = default)
    {
        var company = await context.Companies
            .SingleOrDefaultAsync(c => c.Slug == SeedCompanySlug, ct);

        if (company == null)
        {
            company = new Company
            {
                Id = Guid.NewGuid(),
                Name = new LangStr
                {
                    ["en"] = "Seed Charging Company",
                    ["et"] = "Näidis laadimisettevõte"
                },
                ContactEmail = SeedCompanyOwnerEmail,
                ContactPhone = "+3725000000",
                Slug = SeedCompanySlug,
                IsActive = true
            };

            context.Companies.Add(company);
            await context.SaveChangesAsync(ct);
        }

        var membershipExists = await context.AppUserCompanies.AnyAsync(uc =>
            uc.AppUserId == ownerUserId && uc.CompanyId == company.Id, ct);

        if (!membershipExists)
        {
            context.AppUserCompanies.Add(new AppUserCompany
            {
                Id = Guid.NewGuid(),
                AppUserId = ownerUserId,
                CompanyId = company.Id,
                Role = ECompanyRole.Owner,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync(ct);
        }

        return company.Id;
    }
}
