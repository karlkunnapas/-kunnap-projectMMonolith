using App.BLL.DTOs;
using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace WebApp.Tests.Unit;

public class UnitTestIdentityServicePhase8
{
    [Fact]
    public async Task GetUserCompaniesAsync_ExcludesDeactivatedCompanies()
    {
        await using var context = BuildContext();
        var userManager = BuildUserManager(context);
        await using var unitOfWork = new UnitOfWork(context);
        var auditService = new AuditService(unitOfWork);
        var sut = new IdentityService(null!, userManager, unitOfWork, context, auditService);

        var userId = Guid.NewGuid();
        var activeCompanyId = Guid.NewGuid();
        var inactiveCompanyId = Guid.NewGuid();
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
        await using var unitOfWork = new UnitOfWork(context);
        var auditService = new AuditService(unitOfWork);
        var sut = new IdentityService(null!, userManager, unitOfWork, context, auditService);

        var actorUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
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
        Assert.True(context.AuditLogs.Any(log =>
            log.CompanyId == companyId &&
            log.Action == "UnauthorizedAddAttempt" &&
            log.EntityName == nameof(AppUserCompany)));
    }

    [Fact]
    public async Task AddUserToCompanyAsync_ExistingActiveMembership_ReturnsIdempotentResult()
    {
        await using var context = BuildContext();
        var userManager = BuildUserManager(context);
        await using var unitOfWork = new UnitOfWork(context);
        var auditService = new AuditService(unitOfWork);
        var sut = new IdentityService(null!, userManager, unitOfWork, context, auditService);

        var ownerUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
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
        await using var unitOfWork = new UnitOfWork(context);
        var auditService = new AuditService(unitOfWork);
        var sut = new IdentityService(null!, userManager, unitOfWork, context, auditService);

        var ownerUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
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
        Assert.True(context.AuditLogs.Any(log =>
            log.CompanyId == companyId &&
            log.Action == "UserAddedToCompany" &&
            log.EntityName == nameof(AppUserCompany)));
    }

    [Fact]
    public async Task UpdateCompanyUserRoleAsync_LastOwnerDemotion_FailsWithProtectionError()
    {
        await using var context = BuildContext();
        var userManager = BuildUserManager(context);
        await using var unitOfWork = new UnitOfWork(context);
        var auditService = new AuditService(unitOfWork);
        var sut = new IdentityService(null!, userManager, unitOfWork, context, auditService);

        var ownerUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
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
        await using var unitOfWork = new UnitOfWork(context);
        var auditService = new AuditService(unitOfWork);
        var sut = new IdentityService(null!, userManager, unitOfWork, context, auditService);

        var ownerUserId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
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
}
