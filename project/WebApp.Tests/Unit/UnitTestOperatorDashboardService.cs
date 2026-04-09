using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Tests.Unit;

public class UnitTestOperatorDashboardService
{
    [Fact]
    public async Task GetStationStatusAsync_ReturnsOnlyCompanyStations_AndHealthStates()
    {
        await using var context = BuildContext();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var stationGood = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("Good station"),
            Location = "A",
            Status = EStationStatus.Available,
            PricePerKwh = 0.4m,
            MaxPower = 100m,
            CompanyId = companyA,
            IsActive = true
        };
        var stationCritical = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("Critical station"),
            Location = "B",
            Status = EStationStatus.Maintenance,
            PricePerKwh = 0.4m,
            MaxPower = 100m,
            CompanyId = companyA,
            IsActive = true
        };
        var stationForeign = new ChargingStation
        {
            Id = Guid.NewGuid(),
            Name = new LangStr("Foreign station"),
            Location = "C",
            Status = EStationStatus.Available,
            PricePerKwh = 0.4m,
            MaxPower = 100m,
            CompanyId = companyB,
            IsActive = true
        };

        context.Companies.AddRange(
            new Company { Id = companyA, Name = "A", ContactEmail = "a@test.local", ContactPhone = "+3721", Slug = "company-a", IsActive = true },
            new Company { Id = companyB, Name = "B", ContactEmail = "b@test.local", ContactPhone = "+3722", Slug = "company-b", IsActive = true }
        );
        context.ChargingStations.AddRange(stationGood, stationCritical, stationForeign);
        context.Maintenances.Add(new Maintenance
        {
            Id = Guid.NewGuid(),
            ChargingStationId = stationCritical.Id,
            IssueDescription = "Critical issue",
            Status = EMaintenanceStatus.InProgress,
            ReportedAt = DateTime.UtcNow.AddHours(-3)
        });
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new OperatorDashboardService(uow);

        var result = await service.GetStationStatusAsync(companyA, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Count);
        Assert.DoesNotContain(result.Data, item => item.Name.Contains("Foreign", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Data, item => item.Name.Contains("Critical", StringComparison.OrdinalIgnoreCase) && item.HealthStatus == "Critical");
        Assert.Contains(result.Data, item => item.Name.Contains("Good", StringComparison.OrdinalIgnoreCase) && item.HealthStatus == "Good");
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
