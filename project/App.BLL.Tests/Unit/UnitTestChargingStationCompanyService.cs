using App.BLL.Services;
using App.DAL.EF;
using App.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Moq;
using SC = Shared.Contracts.Charging;
using Shared.Contracts.Companies;

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

        var service = new ChargingStationCompanyService(CreateChargingModuleApiForContext(context).Object, CreateCompaniesApi(companyId).Object, CreateAuditService());

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

        var service = new ChargingStationCompanyService(CreateChargingModuleApiForContext(context).Object, CreateCompaniesApi(companyId).Object, CreateAuditService());

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

        var service = new ChargingStationCompanyService(CreateChargingModuleApiForContext(context).Object, CreateCompaniesApi(companyId).Object, CreateAuditService());

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

        var service = new ChargingStationCompanyService(CreateChargingModuleApiForContext(context).Object, CreateCompaniesApi(ownerCompanyId).Object, CreateAuditService());

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

    private static AuditService CreateAuditService()
    {
        var mediator = new Mock<IMediator>();
        var companiesApi = new Mock<ICompaniesModuleApi>();
        return new AuditService(mediator.Object, companiesApi.Object);
    }

    private static Mock<ICompaniesModuleApi> CreateCompaniesApi(Guid existingCompanyId)
    {
        var mock = new Mock<ICompaniesModuleApi>();
        mock.Setup(x => x.CompanyExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid companyId, CancellationToken _) => companyId == existingCompanyId);
        return mock;
    }

    private static Mock<SC.IChargingModuleApi> CreateChargingModuleApiForContext(AppDbContext context)
    {
        var mock = new Mock<SC.IChargingModuleApi>();

        SC.ChargingStationContract MapStation(ChargingStation station)
        {
            var connectors = context.ChargingStationConnectors
                .Where(link => link.ChargingStationId == station.Id)
                .Join(context.Connectors, link => link.ConnectorId, c => c.Id, (link, c) => new SC.ConnectorContract
                {
                    Id = c.Id,
                    Name = c.Name.Translate() ?? c.Name.ToString() ?? string.Empty,
                    IsActive = c.IsActive
                })
                .ToList();

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
                CompanyId = station.CompanyId,
                Connectors = connectors
            };
        }

        mock.Setup(x => x.GetCompanyStationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid companyId, CancellationToken _) =>
                context.ChargingStations.Where(s => s.CompanyId == companyId).ToList().Select(MapStation).ToList());

        mock.Setup(x => x.GetCompanyStationByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid stationId, Guid companyId, CancellationToken _) =>
            {
                var station = context.ChargingStations.FirstOrDefault(s => s.Id == stationId && s.CompanyId == companyId);
                return station == null ? null : MapStation(station);
            });

        mock.Setup(x => x.GetConnectorsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((bool includeInactive, CancellationToken _) =>
                context.Connectors
                    .Where(c => includeInactive || c.IsActive)
                    .Select(c => new SC.ConnectorContract
                    {
                        Id = c.Id,
                        Name = c.Name.Translate() ?? c.Name.ToString() ?? string.Empty,
                        IsActive = c.IsActive
                    }).ToList());

        mock.Setup(x => x.GetStationAssignedConnectorIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid stationId, CancellationToken _) =>
                context.ChargingStationConnectors.Where(x => x.ChargingStationId == stationId).Select(x => x.ConnectorId).ToList());

        mock.Setup(x => x.SetStationConnectorsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns((Guid stationId, IReadOnlyCollection<Guid> connectorIds, CancellationToken _) =>
            {
                var current = context.ChargingStationConnectors.Where(x => x.ChargingStationId == stationId).ToList();
                context.ChargingStationConnectors.RemoveRange(current.Where(x => !connectorIds.Contains(x.ConnectorId)));
                var existing = current.Select(x => x.ConnectorId).ToHashSet();
                foreach (var connectorId in connectorIds.Where(id => !existing.Contains(id)))
                {
                    context.ChargingStationConnectors.Add(new ChargingStationConnector
                    {
                        Id = Guid.NewGuid(),
                        ChargingStationId = stationId,
                        ConnectorId = connectorId
                    });
                }

                context.SaveChanges();
                return Task.CompletedTask;
            });

        mock.Setup(x => x.CreateCompanyStationAsync(It.IsAny<SC.UpsertCompanyStationContract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SC.UpsertCompanyStationContract request, CancellationToken _) =>
            {
                var station = new ChargingStation
                {
                    Id = request.StationId ?? Guid.NewGuid(),
                    CompanyId = request.CompanyId,
                    Name = new LangStr { ["en"] = request.NameEn, ["et"] = request.NameEt },
                    Location = request.Location,
                    Status = request.Status switch
                    {
                        SC.EStationStatus.Available => EStationStatus.Available,
                        SC.EStationStatus.InUse => EStationStatus.InUse,
                        SC.EStationStatus.Maintenance => EStationStatus.Maintenance,
                        _ => EStationStatus.Available
                    },
                    PricePerKwh = request.PricePerKwh,
                    MaxPower = request.MaxPower,
                    IsActive = request.IsActive
                };
                context.ChargingStations.Add(station);
                context.SaveChanges();
                return MapStation(station);
            });

        mock.Setup(x => x.UpdateCompanyStationAsync(It.IsAny<SC.UpsertCompanyStationContract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SC.UpsertCompanyStationContract request, CancellationToken _) =>
            {
                if (!request.StationId.HasValue) return null;
                var station = context.ChargingStations.FirstOrDefault(s => s.Id == request.StationId.Value && s.CompanyId == request.CompanyId);
                if (station == null) return null;
                station.Name = new LangStr { ["en"] = request.NameEn, ["et"] = request.NameEt };
                station.Location = request.Location;
                station.Status = request.Status switch
                {
                    SC.EStationStatus.Available => EStationStatus.Available,
                    SC.EStationStatus.InUse => EStationStatus.InUse,
                    SC.EStationStatus.Maintenance => EStationStatus.Maintenance,
                    _ => EStationStatus.Available
                };
                station.PricePerKwh = request.PricePerKwh;
                station.MaxPower = request.MaxPower;
                station.IsActive = request.IsActive;
                context.SaveChanges();
                return MapStation(station);
            });

        mock.Setup(x => x.UpdateStationStatusAsync(It.IsAny<Guid>(), It.IsAny<SC.EStationStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid stationId, SC.EStationStatus status, CancellationToken _) =>
            {
                var station = context.ChargingStations.FirstOrDefault(s => s.Id == stationId);
                if (station == null) return false;
                station.Status = status switch
                {
                    SC.EStationStatus.Available => EStationStatus.Available,
                    SC.EStationStatus.InUse => EStationStatus.InUse,
                    SC.EStationStatus.Maintenance => EStationStatus.Maintenance,
                    _ => EStationStatus.Available
                };
                context.SaveChanges();
                return true;
            });

        mock.Setup(x => x.GetMaintenancesByCompanyAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SC.MaintenanceContract>());

        mock.Setup(x => x.DeleteCompanyStationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid stationId, Guid companyId, CancellationToken _) =>
            {
                var station = context.ChargingStations.FirstOrDefault(s => s.Id == stationId && s.CompanyId == companyId);
                if (station == null) return false;
                context.ChargingStations.Remove(station);
                context.SaveChanges();
                return true;
            });

        return mock;
    }
}
