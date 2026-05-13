using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using Microsoft.EntityFrameworkCore;
using Moq;
using SC = Shared.Contracts.Charging;

namespace WebApp.Tests.Unit;

public class UnitTestMaintenanceService
{
    [Fact]
    public async Task CreateMaintenanceAsync_CreatesReportedIssue_AndKeepsStationStatusUnchanged()
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
        var service = new MaintenanceService(CreateChargingModuleApiForContext(context).Object, uow);

        var result = await service.CreateMaintenanceAsync(stationId, companyId, userId, "Connector cable damaged");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(EMaintenanceStatus.Reported, result.Data!.Status);
        Assert.Equal(EStationStatus.Available, context.ChargingStations.Single().Status);
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
        var service = new MaintenanceService(CreateChargingModuleApiForContext(context).Object, uow);

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

    private static Mock<SC.IChargingModuleApi> CreateChargingModuleApiForContext(AppDbContext context)
    {
        var mock = new Mock<SC.IChargingModuleApi>();

        mock.Setup(x => x.GetStationByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid stationId, CancellationToken _) =>
            {
                var station = context.ChargingStations.FirstOrDefault(s => s.Id == stationId);
                if (station == null) return null;
                return new SC.ChargingStationContract
                {
                    Id = station.Id,
                    Name = station.Name.Translate() ?? station.Name.ToString() ?? string.Empty,
                    Location = station.Location,
                    Status = station.Status switch
                    {
                        EStationStatus.Available => SC.EStationStatus.Available,
                        EStationStatus.InUse => SC.EStationStatus.InUse,
                        EStationStatus.Maintenance => SC.EStationStatus.Maintenance,
                        _ => SC.EStationStatus.Available
                    },
                    PricePerKwh = station.PricePerKwh,
                    MaxPower = station.MaxPower,
                    IsActive = station.IsActive,
                    CompanyId = station.CompanyId
                };
            });

        mock.Setup(x => x.CreateMaintenanceAsync(It.IsAny<SC.MaintenanceContract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SC.MaintenanceContract m, CancellationToken _) =>
            {
                context.Maintenances.Add(new Maintenance
                {
                    Id = m.Id,
                    ChargingStationId = m.ChargingStationId,
                    ReportedByUserId = m.ReportedByUserId,
                    IssueDescription = m.IssueDescription,
                    Status = m.Status switch
                    {
                        SC.EMaintenanceStatus.Reported => EMaintenanceStatus.Reported,
                        SC.EMaintenanceStatus.InProgress => EMaintenanceStatus.InProgress,
                        SC.EMaintenanceStatus.Resolved => EMaintenanceStatus.Resolved,
                        _ => EMaintenanceStatus.Reported
                    },
                    ReportedAt = m.ReportedAtUtc,
                    ResolvedAt = m.ResolvedAtUtc,
                    AssignedToUserId = m.AssignedToUserId,
                    Notes = m.Notes
                });
                context.SaveChanges();
                var created = context.Maintenances.Include(x => x.ChargingStation).First(x => x.Id == m.Id);
                return new SC.MaintenanceContract
                {
                    Id = created.Id,
                    CompanyId = created.ChargingStation?.CompanyId,
                    ChargingStationId = created.ChargingStationId,
                    StationName = created.ChargingStation?.Name.Translate() ?? created.ChargingStation?.Name.ToString() ?? string.Empty,
                    ReportedByUserId = created.ReportedByUserId,
                    IssueDescription = created.IssueDescription,
                    Status = m.Status,
                    ReportedAtUtc = created.ReportedAt,
                    ResolvedAtUtc = created.ResolvedAt,
                    AssignedToUserId = created.AssignedToUserId,
                    Notes = created.Notes
                };
            });

        mock.Setup(x => x.GetMaintenanceByIdForCompanyAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid companyId, CancellationToken _) =>
            {
                var m = context.Maintenances.Include(x => x.ChargingStation).FirstOrDefault(x => x.Id == id && x.ChargingStation != null && x.ChargingStation.CompanyId == companyId);
                if (m == null) return null;
                return new SC.MaintenanceContract
                {
                    Id = m.Id,
                    CompanyId = m.ChargingStation?.CompanyId,
                    ChargingStationId = m.ChargingStationId,
                    StationName = m.ChargingStation?.Name.Translate() ?? m.ChargingStation?.Name.ToString() ?? string.Empty,
                    ReportedByUserId = m.ReportedByUserId,
                    IssueDescription = m.IssueDescription,
                    Status = m.Status switch
                    {
                        EMaintenanceStatus.Reported => SC.EMaintenanceStatus.Reported,
                        EMaintenanceStatus.InProgress => SC.EMaintenanceStatus.InProgress,
                        EMaintenanceStatus.Resolved => SC.EMaintenanceStatus.Resolved,
                        _ => SC.EMaintenanceStatus.Reported
                    },
                    ReportedAtUtc = m.ReportedAt,
                    ResolvedAtUtc = m.ResolvedAt,
                    AssignedToUserId = m.AssignedToUserId,
                    Notes = m.Notes
                };
            });

        mock.Setup(x => x.UpdateMaintenanceStatusAsync(It.IsAny<Guid>(), It.IsAny<SC.EMaintenanceStatus>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, SC.EMaintenanceStatus status, string? notes, DateTime? resolvedAt, CancellationToken _) =>
            {
                var m = context.Maintenances.FirstOrDefault(x => x.Id == id);
                if (m == null) return false;
                m.Status = status switch
                {
                    SC.EMaintenanceStatus.Reported => EMaintenanceStatus.Reported,
                    SC.EMaintenanceStatus.InProgress => EMaintenanceStatus.InProgress,
                    SC.EMaintenanceStatus.Resolved => EMaintenanceStatus.Resolved,
                    _ => EMaintenanceStatus.Reported
                };
                m.Notes = notes;
                m.ResolvedAt = resolvedAt;
                context.SaveChanges();
                return true;
            });

        mock.Setup(x => x.UpdateStationStatusAsync(It.IsAny<Guid>(), It.IsAny<SC.EStationStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid stationId, SC.EStationStatus status, CancellationToken _) =>
            {
                var s = context.ChargingStations.FirstOrDefault(x => x.Id == stationId);
                if (s == null) return false;
                s.Status = status switch
                {
                    SC.EStationStatus.Available => EStationStatus.Available,
                    SC.EStationStatus.InUse => EStationStatus.InUse,
                    SC.EStationStatus.Maintenance => EStationStatus.Maintenance,
                    _ => EStationStatus.Available
                };
                context.SaveChanges();
                return true;
            });

        return mock;
    }
}
