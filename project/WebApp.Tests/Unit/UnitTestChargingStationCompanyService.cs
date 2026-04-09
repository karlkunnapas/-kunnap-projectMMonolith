using App.BLL.Services;
using App.DAL.EF;
using App.DAL.EF.Repositories.Implementations;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Tests.Unit;

public class UnitTestChargingStationCompanyService
{
    [Fact]
    public async Task CreateStationAsync_SetsCompanyTranslations_AndConnectorAssignments()
    {
        await using var context = BuildContext();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var connectorA = new Connector { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "CCS", ["et"] = "CCS" }, IsActive = true };
        var connectorB = new Connector { Id = Guid.NewGuid(), Name = new LangStr { ["en"] = "Type 2", ["et"] = "Type 2" }, IsActive = true };

        context.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Ops",
            ContactEmail = "ops@test.local",
            ContactPhone = "+3725000000",
            Slug = "ops-company-create",
            IsActive = true
        });
        context.Connectors.AddRange(connectorA, connectorB);
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new ChargingStationCompanyService(uow, new AuditService(uow));

        var result = await service.CreateStationAsync(companyId, userId, "owner@test.local", new App.BLL.DTOs.CompanyStationUpsertDto
        {
            NameEn = "North Hub",
            NameEt = "Pohja keskus",
            Location = "Tallinn",
            PricePerKwh = 0.42m,
            MaxPower = 150m,
            Status = EStationStatus.Available,
            IsActive = true,
            SelectedConnectorIds = new List<Guid> { connectorA.Id, connectorB.Id }
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var station = await context.ChargingStations
            .Include(s => s.ChargingStationConnectors!)
            .SingleAsync();

        Assert.Equal(companyId, station.CompanyId);
        Assert.Equal("North Hub", station.Name.Translate("en"));
        Assert.Equal("Pohja keskus", station.Name.Translate("et"));
        Assert.Equal(2, station.ChargingStationConnectors!.Count);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidStatus_ReturnsValidationError()
    {
        await using var context = BuildContext();
        var companyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Ops",
            ContactEmail = "ops2@test.local",
            ContactPhone = "+3725000001",
            Slug = "ops-company-status",
            IsActive = true
        });
        context.ChargingStations.Add(new ChargingStation
        {
            Id = stationId,
            Name = new LangStr("Station"),
            Location = "Tartu",
            Status = EStationStatus.Available,
            PricePerKwh = 0.35m,
            MaxPower = 100m,
            CompanyId = companyId,
            IsActive = true
        });
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new ChargingStationCompanyService(uow, new AuditService(uow));

        var result = await service.UpdateStatusAsync(stationId, companyId, userId, "owner@test.local", (EStationStatus)999);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Code == "VALIDATION");
    }

    [Fact]
    public async Task AssignConnectorAsync_DuplicateAssignment_IsRejected()
    {
        await using var context = BuildContext();
        var companyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Ops",
            ContactEmail = "ops3@test.local",
            ContactPhone = "+3725000002",
            Slug = "ops-company-connector",
            IsActive = true
        });
        context.Connectors.Add(new Connector
        {
            Id = connectorId,
            Name = new LangStr("CCS"),
            IsActive = true
        });
        context.ChargingStations.Add(new ChargingStation
        {
            Id = stationId,
            Name = new LangStr("Station"),
            Location = "Parnu",
            Status = EStationStatus.Available,
            PricePerKwh = 0.30m,
            MaxPower = 75m,
            CompanyId = companyId,
            IsActive = true
        });
        context.ChargingStationConnectors.Add(new ChargingStationConnector
        {
            Id = Guid.NewGuid(),
            ChargingStationId = stationId,
            ConnectorId = connectorId
        });
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new ChargingStationCompanyService(uow, new AuditService(uow));

        var result = await service.AssignConnectorAsync(stationId, companyId, userId, "owner@test.local", connectorId);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Code == "VALIDATION");
    }

    [Fact]
    public async Task UpdateStationAsync_ForeignCompany_ReturnsForbidden()
    {
        await using var context = BuildContext();
        var ownerCompanyId = Guid.NewGuid();
        var foreignCompanyId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Companies.AddRange(
            new Company { Id = ownerCompanyId, Name = "Owner", ContactEmail = "owner@test.local", ContactPhone = "+3725550001", Slug = "owner-company", IsActive = true },
            new Company { Id = foreignCompanyId, Name = "Foreign", ContactEmail = "foreign@test.local", ContactPhone = "+3725550002", Slug = "foreign-company", IsActive = true }
        );
        context.Connectors.Add(new Connector { Id = Guid.NewGuid(), Name = new LangStr("CCS"), IsActive = true });
        context.ChargingStations.Add(new ChargingStation
        {
            Id = stationId,
            Name = new LangStr("Foreign station"),
            Location = "Rakvere",
            Status = EStationStatus.Available,
            PricePerKwh = 0.28m,
            MaxPower = 60m,
            CompanyId = foreignCompanyId,
            IsActive = true
        });
        await context.SaveChangesAsync();

        await using var uow = new UnitOfWork(context);
        var service = new ChargingStationCompanyService(uow, new AuditService(uow));

        var result = await service.UpdateStationAsync(stationId, ownerCompanyId, userId, "owner@test.local", new App.BLL.DTOs.CompanyStationUpsertDto
        {
            NameEn = "Updated",
            NameEt = "Uuendatud",
            Location = "Rakvere",
            PricePerKwh = 0.29m,
            MaxPower = 65m,
            Status = EStationStatus.InUse,
            IsActive = true
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Code == "FORBIDDEN");
    }

    private static AppDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
