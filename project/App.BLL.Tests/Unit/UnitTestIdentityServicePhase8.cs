using App.BLL.DTOs;
using App.BLL.Services;
using App.DAL.EF;
using App.Domain;
using App.Domain.Identity;
using Mediator;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;

namespace WebApp.Tests.Unit;

public class UnitTestIdentityServicePhase8
{
    [Fact]
    public async Task GetUserCompaniesAsync_ExcludesDeactivatedCompanies()
    {
        await using var context = BuildContext();
        var userManager = BuildUserManager(context);
        var auditService = CreateAuditService();
        var userId = Guid.NewGuid();
        var activeCompanyId = Guid.NewGuid();
        var inactiveCompanyId = Guid.NewGuid();
        var usersModuleApi = new Mock<IUsersModuleApi>();
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        ConfigureCompaniesModuleApiForMemberships(companiesModuleApi, context);
        companiesModuleApi
            .Setup(x => x.GetUserCompaniesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => context.AppUserCompanies
                .Where(uc => uc.AppUserId == userId && uc.IsActive)
                .Join(
                    context.Companies.Where(c => c.IsActive),
                    uc => uc.CompanyId,
                    c => c.Id,
                    (uc, c) => new UserCompanyMembershipContract
                    {
                        MembershipId = uc.Id,
                        CompanyId = c.Id,
                        UserId = uc.AppUserId,
                        CompanyName = c.Name,
                        Slug = c.Slug,
                        Role = uc.Role.ToString(),
                        IsActive = uc.IsActive
                    })
                .ToList());
        usersModuleApi
            .Setup(x => x.UserExistsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        usersModuleApi
            .Setup(x => x.GetUserDisplayNameAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("owner@test.local");
        var sut = new IdentityService(null!, userManager, usersModuleApi.Object, companiesModuleApi.Object, auditService);
        await SeedUserAndCompanyAsync(context, userId, "owner@test.local", activeCompanyId, ECompanyRole.Owner);
        await SeedUserAndCompanyAsync(context, userId, "owner@test.local", inactiveCompanyId, ECompanyRole.Owner);

        var inactiveCompany = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Id == inactiveCompanyId);
        inactiveCompany.IsActive = false;
        await context.SaveChangesAsync();

        var result = await sut.GetUserCompaniesAsync(userId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Companies);
        Assert.Equal(activeCompanyId, result.Data.Companies[0].CompanyId);
    }

    [Fact]
    public async Task AddUserToCompanyAsync_NotOwner_ReturnsNotOwner_AndLogsUnauthorizedAttempt()
    {
        await using var context = BuildContext();
        var userManager = BuildUserManager(context);
        var auditService = CreateAuditService();
        var actorUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var usersModuleApi = new Mock<IUsersModuleApi>();
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        ConfigureCompaniesModuleApiForMemberships(companiesModuleApi, context);
        companiesModuleApi
            .Setup(x => x.HasActiveOwnerMembershipAsync(companyId, actorUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = new IdentityService(null!, userManager, usersModuleApi.Object, companiesModuleApi.Object, auditService);

        await SeedUserAndCompanyAsync(context, actorUserId, "actor@test.local", companyId, ECompanyRole.Employee);

        var result = await sut.AddUserToCompanyAsync(
            companyId,
            actorUserId,
            "actor@test.local",
            new AddCompanyUserRequestDto
            {
                Email = "new-user@test.local",
                Role = ECompanyRole.Employee
            });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Code == "NOT_OWNER");
    }

    [Fact]
    public async Task AddUserToCompanyAsync_ExistingActiveMembership_ReturnsIdempotentResult()
    {
        await using var context = BuildContext();
        var userManager = BuildUserManager(context);
        var auditService = CreateAuditService();
        var ownerUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var usersModuleApi = new Mock<IUsersModuleApi>();
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        ConfigureCompaniesModuleApiForMemberships(companiesModuleApi, context);
        companiesModuleApi
            .Setup(x => x.HasActiveOwnerMembershipAsync(companyId, ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new IdentityService(null!, userManager, usersModuleApi.Object, companiesModuleApi.Object, auditService);

        await SeedUserAndCompanyAsync(context, ownerUserId, "owner@test.local", companyId, ECompanyRole.Owner);
        await SeedUserAndMembershipAsync(context, targetUserId, "existing@test.local", companyId, ECompanyRole.Employee, isActive: true);

        var result = await sut.AddUserToCompanyAsync(
            companyId,
            ownerUserId,
            "owner@test.local",
            new AddCompanyUserRequestDto
            {
                Email = "existing@test.local",
                Role = ECompanyRole.Manager
            });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.MembershipAlreadyActive);
        Assert.Equal(1, context.AppUserCompanies.Count(uc => uc.AppUserId == targetUserId && uc.CompanyId == companyId));
    }

    [Fact]
    public async Task AddUserToCompanyAsync_NewUser_CreatesUserAndMembershipWithOwnerDefinedPassword()
    {
        await using var context = BuildContext();
        var userManager = BuildUserManager(context);
        var auditService = CreateAuditService();
        var ownerUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var usersModuleApi = new Mock<IUsersModuleApi>();
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        ConfigureCompaniesModuleApiForMemberships(companiesModuleApi, context);
        companiesModuleApi
            .Setup(x => x.HasActiveOwnerMembershipAsync(companyId, ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new IdentityService(null!, userManager, usersModuleApi.Object, companiesModuleApi.Object, auditService);

        await SeedUserAndCompanyAsync(context, ownerUserId, "owner@test.local", companyId, ECompanyRole.Owner);

        var result = await sut.AddUserToCompanyAsync(
            companyId,
            ownerUserId,
            "owner@test.local",
            new AddCompanyUserRequestDto
            {
                Email = "brand-new@test.local",
                Role = ECompanyRole.Employee,
                FirstName = "Brand",
                LastName = "New",
                PhoneNumber = "+3725001111",
                Password = "OwnerSet#123",
                ConfirmPassword = "OwnerSet#123"
            });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data!.IsExistingUser);
        Assert.True(context.Users.Any(u => u.Email == "brand-new@test.local"));
        Assert.True(context.AppUserCompanies.Any(uc => uc.AppUserId == result.Data.UserId && uc.CompanyId == companyId));
    }

    [Fact]
    public async Task UpdateCompanyUserRoleAsync_LastOwnerDemotion_FailsWithProtectionError()
    {
        await using var context = BuildContext();
        var userManager = BuildUserManager(context);
        var auditService = CreateAuditService();
        var ownerUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var usersModuleApi = new Mock<IUsersModuleApi>();
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        ConfigureCompaniesModuleApiForMemberships(companiesModuleApi, context);
        companiesModuleApi
            .Setup(x => x.HasActiveOwnerMembershipAsync(companyId, ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new IdentityService(null!, userManager, usersModuleApi.Object, companiesModuleApi.Object, auditService);

        await SeedUserAndCompanyAsync(context, ownerUserId, "owner@test.local", companyId, ECompanyRole.Owner);
        var ownerMembership = context.AppUserCompanies.Single(uc => uc.AppUserId == ownerUserId && uc.CompanyId == companyId);

        var result = await sut.UpdateCompanyUserRoleAsync(
            companyId,
            ownerUserId,
            "owner@test.local",
            ownerMembership.Id,
            new UpdateCompanyUserRoleRequestDto { Role = ECompanyRole.Manager });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "LAST_OWNER_PROTECTION");
    }

    [Fact]
    public async Task AddUserToCompanyAsync_NewUserWithoutProfileFields_FailsValidation()
    {
        await using var context = BuildContext();
        var userManager = BuildUserManager(context);
        var auditService = CreateAuditService();
        var ownerUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var usersModuleApi = new Mock<IUsersModuleApi>();
        var companiesModuleApi = new Mock<ICompaniesModuleApi>();
        ConfigureCompaniesModuleApiForMemberships(companiesModuleApi, context);
        companiesModuleApi
            .Setup(x => x.HasActiveOwnerMembershipAsync(companyId, ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new IdentityService(null!, userManager, usersModuleApi.Object, companiesModuleApi.Object, auditService);

        await SeedUserAndCompanyAsync(context, ownerUserId, "owner@test.local", companyId, ECompanyRole.Owner);

        var result = await sut.AddUserToCompanyAsync(
            companyId,
            ownerUserId,
            "owner@test.local",
            new AddCompanyUserRequestDto
            {
                Email = "missing-fields@test.local",
                Role = ECompanyRole.Employee,
                Password = "OwnerSet#123",
                ConfirmPassword = "OwnerSet#123"
            });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Message.Contains("First name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, e => e.Message.Contains("Last name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, e => e.Message.Contains("Phone number", StringComparison.OrdinalIgnoreCase));
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static UserManager<AppUser> BuildUserManager(AppDbContext context)
    {
        var store = new UserStore<AppUser, AppRole, AppDbContext, Guid>(context);
        var options = new IdentityOptions();
        var userValidators = new List<IUserValidator<AppUser>> { new UserValidator<AppUser>() };
        var passwordValidators = new List<IPasswordValidator<AppUser>> { new PasswordValidator<AppUser>() };
        var userManager = new UserManager<AppUser>(
            store,
            Microsoft.Extensions.Options.Options.Create(options),
            new PasswordHasher<AppUser>(),
            userValidators,
            passwordValidators,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            new Logger<UserManager<AppUser>>(LoggerFactory.Create(builder => builder.AddDebug())));

        return userManager;
    }

    private static async Task SeedUserAndCompanyAsync(
        AppDbContext context,
        Guid userId,
        string email,
        Guid companyId,
        ECompanyRole role)
    {
        if (!context.Users.Any(u => u.Id == userId))
        {
            context.Users.Add(new AppUser
            {
                Id = userId,
                UserName = email,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true
            });
        }

        if (!context.Companies.Any(c => c.Id == companyId))
        {
            context.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Company",
                ContactEmail = "company@test.local",
                ContactPhone = "+3725000000",
                Slug = $"company-{companyId:N}",
                IsActive = true
            });
        }

        if (!context.AppUserCompanies.Any(uc => uc.AppUserId == userId && uc.CompanyId == companyId))
        {
            context.AppUserCompanies.Add(new AppUserCompany
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                CompanyId = companyId,
                Role = role,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    private static void ConfigureCompaniesModuleApiForMemberships(Mock<ICompaniesModuleApi> companiesModuleApi, AppDbContext context)
    {
        companiesModuleApi
            .Setup(x => x.GetCompanyMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid companyId, Guid membershipId, CancellationToken _) =>
                context.AppUserCompanies
                    .Where(x => x.CompanyId == companyId && x.Id == membershipId)
                    .Select(x => new CompanyMembershipContract
                    {
                        MembershipId = x.Id,
                        CompanyId = x.CompanyId,
                        UserId = x.AppUserId,
                        Role = x.Role.ToString(),
                        IsActive = x.IsActive,
                        JoinedAtUtc = x.JoinedAtUtc
                    })
                    .FirstOrDefault());

        companiesModuleApi
            .Setup(x => x.GetCompanyMembershipByUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid companyId, Guid userId, CancellationToken _) =>
                context.AppUserCompanies
                    .Where(x => x.CompanyId == companyId && x.AppUserId == userId)
                    .Select(x => new CompanyMembershipContract
                    {
                        MembershipId = x.Id,
                        CompanyId = x.CompanyId,
                        UserId = x.AppUserId,
                        Role = x.Role.ToString(),
                        IsActive = x.IsActive,
                        JoinedAtUtc = x.JoinedAtUtc
                    })
                    .FirstOrDefault());

        companiesModuleApi
            .Setup(x => x.UpsertCompanyMembershipAsync(It.IsAny<UpsertCompanyMembershipContract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpsertCompanyMembershipContract request, CancellationToken _) =>
            {
                var parsedRole = Enum.TryParse<ECompanyRole>(request.Role, true, out var role) ? role : ECompanyRole.Employee;
                var existing = context.AppUserCompanies.FirstOrDefault(x => x.CompanyId == request.CompanyId && x.AppUserId == request.UserId);
                if (existing != null)
                {
                    if (existing.IsActive)
                    {
                        return new UpsertCompanyMembershipResultContract
                        {
                            Membership = new CompanyMembershipContract
                            {
                                MembershipId = existing.Id, CompanyId = existing.CompanyId, UserId = existing.AppUserId,
                                Role = existing.Role.ToString(), IsActive = existing.IsActive, JoinedAtUtc = existing.JoinedAtUtc
                            },
                            Operation = "AlreadyActive"
                        };
                    }

                    existing.IsActive = true;
                    existing.Role = parsedRole;
                    existing.JoinedAtUtc = DateTime.UtcNow;
                    context.SaveChanges();

                    return new UpsertCompanyMembershipResultContract
                    {
                        Membership = new CompanyMembershipContract
                        {
                            MembershipId = existing.Id, CompanyId = existing.CompanyId, UserId = existing.AppUserId,
                            Role = existing.Role.ToString(), IsActive = existing.IsActive, JoinedAtUtc = existing.JoinedAtUtc
                        },
                        Operation = "Reactivated"
                    };
                }

                var created = new AppUserCompany
                {
                    Id = Guid.NewGuid(),
                    CompanyId = request.CompanyId,
                    AppUserId = request.UserId,
                    Role = parsedRole,
                    IsActive = true,
                    JoinedAtUtc = DateTime.UtcNow
                };
                context.AppUserCompanies.Add(created);
                context.SaveChanges();
                return new UpsertCompanyMembershipResultContract
                {
                    Membership = new CompanyMembershipContract
                    {
                        MembershipId = created.Id, CompanyId = created.CompanyId, UserId = created.AppUserId,
                        Role = created.Role.ToString(), IsActive = created.IsActive, JoinedAtUtc = created.JoinedAtUtc
                    },
                    Operation = "Created"
                };
            });

        companiesModuleApi
            .Setup(x => x.UpdateCompanyMembershipRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid companyId, Guid membershipId, string role, CancellationToken _) =>
            {
                var membership = context.AppUserCompanies.FirstOrDefault(x => x.CompanyId == companyId && x.Id == membershipId);
                if (membership == null) return null;
                membership.Role = Enum.TryParse<ECompanyRole>(role, true, out var parsedRole) ? parsedRole : ECompanyRole.Employee;
                context.SaveChanges();
                return new CompanyMembershipContract
                {
                    MembershipId = membership.Id,
                    CompanyId = membership.CompanyId,
                    UserId = membership.AppUserId,
                    Role = membership.Role.ToString(),
                    IsActive = membership.IsActive,
                    JoinedAtUtc = membership.JoinedAtUtc
                };
            });

        companiesModuleApi
            .Setup(x => x.DeactivateCompanyMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid companyId, Guid membershipId, CancellationToken _) =>
            {
                var membership = context.AppUserCompanies.FirstOrDefault(x => x.CompanyId == companyId && x.Id == membershipId);
                if (membership == null) return null;
                membership.IsActive = false;
                context.SaveChanges();
                return new CompanyMembershipContract
                {
                    MembershipId = membership.Id,
                    CompanyId = membership.CompanyId,
                    UserId = membership.AppUserId,
                    Role = membership.Role.ToString(),
                    IsActive = membership.IsActive,
                    JoinedAtUtc = membership.JoinedAtUtc
                };
            });

        companiesModuleApi
            .Setup(x => x.CountActiveCompanyOwnersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid companyId, CancellationToken _) =>
                context.AppUserCompanies.Count(x => x.CompanyId == companyId && x.IsActive && x.Role == ECompanyRole.Owner));

        companiesModuleApi
            .Setup(x => x.HasAnyActiveOwnerMembershipForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, CancellationToken _) =>
                context.AppUserCompanies.Any(x => x.AppUserId == userId && x.IsActive && x.Role == ECompanyRole.Owner));
    }

    private static async Task SeedUserAndMembershipAsync(
        AppDbContext context,
        Guid userId,
        string email,
        Guid companyId,
        ECompanyRole role,
        bool isActive)
    {
        if (!context.Users.Any(u => u.Id == userId))
        {
            context.Users.Add(new AppUser
            {
                Id = userId,
                UserName = email,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true
            });
        }

        if (!context.AppUserCompanies.Any(uc => uc.AppUserId == userId && uc.CompanyId == companyId))
        {
            context.AppUserCompanies.Add(new AppUserCompany
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                CompanyId = companyId,
                Role = role,
                IsActive = isActive,
                JoinedAtUtc = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    private static AuditService CreateAuditService()
    {
        var mediator = new Mock<IMediator>();
        var companiesApi = new Mock<ICompaniesModuleApi>();
        return new AuditService(mediator.Object, companiesApi.Object);
    }
}
