using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Tests.Unit;

public class UnitTestMaintenanceService
{
    [Fact]
    public async Task CreateMaintenanceAsync_CreatesReportedIssue_AndSetsStationToMaintenance()
    {
        await using var context = BuildContext();
        var companyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Ops",
            ContactEmail = "ops@test.local",
            ContactPhone = "+3725000000",
            Slug = "ops-company",
            IsActive = true
        });
        context.ChargingStations.Add(new ChargingStation
        {
            Id = stationId,
            Name = new LangStr("Ops station"),
            Location = "Tallinn",
            Status = EStationStatus.Available,
            PricePerKwh = 0.4m,
            MaxPower = 100m,
            CompanyId = companyId,
            IsActive = true
        });
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new MaintenanceService(uow);

        var result = await service.CreateMaintenanceAsync(stationId, companyId, userId, "Connector cable damaged");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(EMaintenanceStatus.Reported, result.Data!.Status);
        Assert.Equal(EStationStatus.Maintenance, context.ChargingStations.Single().Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_Resolved_SetsResolvedAt_AndRestoresStationAvailability()
    {
        await using var context = BuildContext();
        var companyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var issueId = Guid.NewGuid();

        context.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Ops",
            ContactEmail = "ops2@test.local",
            ContactPhone = "+3725000001",
            Slug = "ops-company-2",
            IsActive = true
        });
        context.ChargingStations.Add(new ChargingStation
        {
            Id = stationId,
            Name = new LangStr("Ops station 2"),
            Location = "Tartu",
            Status = EStationStatus.Maintenance,
            PricePerKwh = 0.4m,
            MaxPower = 150m,
            CompanyId = companyId,
            IsActive = true
        });
        context.Maintenances.Add(new Maintenance
        {
            Id = issueId,
            ChargingStationId = stationId,
            ReportedByUserId = userId,
            IssueDescription = "Power module fault",
            Status = EMaintenanceStatus.Reported,
            ReportedAt = DateTime.UtcNow.AddMinutes(-30)
        });
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new MaintenanceService(uow);

        var result = await service.UpdateStatusAsync(issueId, companyId, userId, EMaintenanceStatus.Resolved, "Replaced faulty module");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(EMaintenanceStatus.Resolved, result.Data!.Status);
        Assert.NotNull(result.Data.ResolvedAtUtc);
        Assert.Equal(EStationStatus.Available, context.ChargingStations.Single().Status);
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
