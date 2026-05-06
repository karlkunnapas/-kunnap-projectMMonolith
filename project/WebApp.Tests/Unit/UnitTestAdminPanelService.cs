using App.BLL.DTOs;
using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WebApp.Tests.Unit;

public class UnitTestAdminPanelService
{
    [Fact]
    public async Task GetDashboardAsync_ReturnsExpectedCounts()
    {
        await using var context = BuildContext();
        var fromUtc = DateTime.UtcNow.AddDays(-30);
        var toUtc = DateTime.UtcNow;

        var companyA = new Company
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("Company A"),
            ContactEmail = "a@test.local",
            ContactPhone = "+3721111111",
            Slug = "company-a",
            IsActive = true
        };
        var companyB = new Company
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("Company B"),
            ContactEmail = "b@test.local",
            ContactPhone = "+3722222222",
            Slug = "company-b",
            IsActive = false
        };

        var companyUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "company-user@test.local",
            Email = "company-user@test.local",
            NormalizedEmail = "COMPANY-USER@TEST.LOCAL",
            EmailConfirmed = true
        };
        var clientUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "client-user@test.local",
            Email = "client-user@test.local",
            NormalizedEmail = "CLIENT-USER@TEST.LOCAL",
            EmailConfirmed = true
        };

        context.Companies.AddRange(companyA, companyB);
        context.Users.AddRange(companyUser, clientUser);
        context.AppUserCompanies.Add(new AppUserCompany
        {
            Id = Guid.NewGuid(),
            AppUserId = companyUser.Id,
            CompanyId = companyA.Id,
            Role = ECompanyRole.Owner,
            IsActive = true,
            JoinedAtUtc = DateTime.UtcNow
        });

        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("Station A"),
            Location = "Tallinn",
            Status = EStationStatus.Available,
            PricePerKwh = 0.35m,
            MaxPower = 50m,
            CompanyId = companyA.Id,
            IsActive = true
        };
        context.ChargingStations.Add(station);
        context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = clientUser.Id,
            ChargingStationId = station.Id,
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow,
            EstimatedCost = 8m,
            Status = EReservationStatus.Active
        });
        await context.SaveChangesAsync();

        var userManager = BuildUserManager(context);
        await using var unitOfWork = new UnitOfWork(context);
        var auditService = new AuditService(unitOfWork);
        var sut = new AdminPanelService(unitOfWork, userManager, auditService);

        var result = await sut.GetDashboardAsync(fromUtc, toUtc);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.ReservationsInPeriod);
        Assert.Equal(2, result.Data.TotalCompanies);
        Assert.Equal(1, result.Data.TotalCompanyUsers);
        Assert.Equal(1, result.Data.TotalClientUsers);
    }

    [Fact]
    public async Task SetCompanyActivationAsync_UpdatesState_AndWritesAudit()
    {
        await using var context = BuildContext();
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("Activation Co"),
            ContactEmail = "activation@test.local",
            ContactPhone = "+3723333333",
            Slug = "activation-co",
            IsActive = true
        };
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        var userManager = BuildUserManager(context);
        await using var unitOfWork = new UnitOfWork(context);
        var auditService = new AuditService(unitOfWork);
        var sut = new AdminPanelService(unitOfWork, userManager, auditService);

        var result = await sut.SetCompanyActivationAsync(company.Id, false, "admin@test.local");

        Assert.True(result.Success);
        var persisted = await context.Companies.IgnoreQueryFilters().FirstAsync(c => c.Id == company.Id);
        Assert.False(persisted.IsActive);
        Assert.True(context.AuditLogs.Any(log =>
            log.CompanyId == company.Id &&
            log.Action == "CompanyInactivated" &&
            log.EntityName == nameof(Company)));
    }

    [Fact]
    public async Task GetAuditLogsAsync_AppliesActorFilter()
    {
        await using var context = BuildContext();
        var companyId = Guid.NewGuid();
        context.AuditLogs.AddRange(
            new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserName = "admin-user@test.local",
                EntityName = nameof(Company),
                EntityId = Guid.NewGuid(),
                Action = "CompanyActivated",
                AtUtc = DateTime.UtcNow.AddMinutes(-10)
            },
            new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserName = "other-user@test.local",
                EntityName = nameof(Company),
                EntityId = Guid.NewGuid(),
                Action = "CompanyInactivated",
                AtUtc = DateTime.UtcNow.AddMinutes(-5)
            });
        await context.SaveChangesAsync();

        var userManager = BuildUserManager(context);
        await using var unitOfWork = new UnitOfWork(context);
        var auditService = new AuditService(unitOfWork);
        var sut = new AdminPanelService(unitOfWork, userManager, auditService);

        var result = await sut.GetAuditLogsAsync(new AdminAuditLogFilterDto
        {
            Actor = "admin-user",
            Page = 1,
            PageSize = 50
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Items);
        Assert.Equal("admin-user@test.local", result.Data.Items[0].UserName);
    }

    [Fact]
    public async Task GetStationsAsync_SearchByCompany_ReturnsMatchingStations()
    {
        await using var context = BuildContext();
        var companyA = new Company
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("North Energy"),
            ContactEmail = "north@test.local",
            ContactPhone = "+3727777777",
            Slug = "north-energy",
            IsActive = true
        };
        var companyB = new Company
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("South Energy"),
            ContactEmail = "south@test.local",
            ContactPhone = "+3728888888",
            Slug = "south-energy",
            IsActive = true
        };

        context.Companies.AddRange(companyA, companyB);
        context.ChargingStations.AddRange(
            new ChargingStation
            {
                Id = Guid.NewGuid(),
                Name = new LangStr("North Hub"),
                Location = "Tallinn",
                Status = EStationStatus.Available,
                PricePerKwh = 0.30m,
                MaxPower = 150m,
                IsActive = true,
                CompanyId = companyA.Id
            },
            new ChargingStation
            {
                Id = Guid.NewGuid(),
                Name = new LangStr("South Hub"),
                Location = "Tartu",
                Status = EStationStatus.InUse,
                PricePerKwh = 0.35m,
                MaxPower = 120m,
                IsActive = false,
                CompanyId = companyB.Id
            });
        await context.SaveChangesAsync();

        var userManager = BuildUserManager(context);
        await using var unitOfWork = new UnitOfWork(context);
        var auditService = new AuditService(unitOfWork);
        var sut = new AdminPanelService(unitOfWork, userManager, auditService);

        var result = await sut.GetStationsAsync("north");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Items);
        Assert.Equal("North Energy", result.Data.Items[0].CompanyName);
        Assert.Equal("North Hub", result.Data.Items[0].Name);
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
        return new UserManager<AppUser>(
            store,
            Microsoft.Extensions.Options.Options.Create(options),
            new PasswordHasher<AppUser>(),
            userValidators,
            passwordValidators,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            new Logger<UserManager<AppUser>>(LoggerFactory.Create(builder => builder.AddDebug())));
    }
}
