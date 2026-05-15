using Microsoft.EntityFrameworkCore;
using Modules.Charging.Domain;
using Shared.Contracts;

namespace Modules.Charging.Infrastructure.Seeding;

internal static class ChargingModuleDataSeeder
{
    private static readonly HashSet<string> SeedStationLocations = new(StringComparer.OrdinalIgnoreCase)
    {
        "Kesklinna tn 2",
        "Põhja tn 13",
        "Lennujaama 42"
    };

    internal static async Task SeedDataAsync(ChargingDbContext context, Guid companyId, CancellationToken ct = default)
    {
        var type2 = await EnsureConnectorAsync(context, "Type 2 AC", "Type 2 AC", ct);
        var ccs = await EnsureConnectorAsync(context, "CCS", "CCS", ct);
        var tesla = await EnsureConnectorAsync(context, "Tesla", "Tesla", ct);
        var chademo = await EnsureConnectorAsync(context, "CHAdeMO", "CHAdeMO", ct);

        var downtownStation = await EnsureStationAsync(
            context,
            "Kesklinna tn 2",
            "Downtown Charging Hub",
            "Kesklinna laadimiskeskus",
            EStationStatus.Available,
            0.40m,
            350m,
            companyId,
            ct);

        var northStation = await EnsureStationAsync(
            context,
            "Põhja tn 13",
            "North Side Charger",
            "Põhja laadija",
            EStationStatus.InUse,
            0.40m,
            150m,
            companyId,
            ct);

        var airportStation = await EnsureStationAsync(
            context,
            "Lennujaama 42",
            "Airport Charging Point",
            "Lennujaama laadimispunkt",
            EStationStatus.Maintenance,
            0.40m,
            50m,
            companyId,
            ct);

        await AddStationConnectorIfMissingAsync(context, downtownStation.Id, type2.Id, ct);
        await AddStationConnectorIfMissingAsync(context, downtownStation.Id, ccs.Id, ct);
        await AddStationConnectorIfMissingAsync(context, downtownStation.Id, tesla.Id, ct);
        await AddStationConnectorIfMissingAsync(context, northStation.Id, type2.Id, ct);
        await AddStationConnectorIfMissingAsync(context, northStation.Id, ccs.Id, ct);
        await AddStationConnectorIfMissingAsync(context, airportStation.Id, ccs.Id, ct);
        await AddStationConnectorIfMissingAsync(context, airportStation.Id, tesla.Id, ct);
        await AddStationConnectorIfMissingAsync(context, airportStation.Id, chademo.Id, ct);

        var unownedSeedStations = await context.ChargingStations
            .Where(s => s.CompanyId == null)
            .ToListAsync(ct);

        var needsSave = false;
        foreach (var station in unownedSeedStations.Where(s =>
                     SeedStationLocations.Contains((s.Location ?? string.Empty).Trim())))
        {
            station.CompanyId = companyId;
            needsSave = true;
        }

        if (needsSave)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private static async Task<Connector> EnsureConnectorAsync(
        ChargingDbContext context,
        string nameEn,
        string nameEt,
        CancellationToken ct)
    {
        var connectors = await context.Connectors.ToListAsync(ct);
        var existing = connectors.SingleOrDefault(c => MatchLocalizedName(c.Name, nameEn));
        if (existing != null)
        {
            return existing;
        }

        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            Name = new LangStr
            {
                ["en"] = nameEn,
                ["et"] = nameEt
            },
            IsActive = true
        };

        context.Connectors.Add(connector);
        await context.SaveChangesAsync(ct);
        return connector;
    }

    private static async Task<ChargingStation> EnsureStationAsync(
        ChargingDbContext context,
        string location,
        string nameEn,
        string nameEt,
        EStationStatus status,
        decimal pricePerKwh,
        decimal maxPower,
        Guid companyId,
        CancellationToken ct)
    {
        var station = await context.ChargingStations
            .SingleOrDefaultAsync(s => s.Location.Trim().ToLower() == location.Trim().ToLower(), ct);

        if (station == null)
        {
            station = new ChargingStation
            {
                Id = Guid.NewGuid(),
                Name = new LangStr
                {
                    ["en"] = nameEn,
                    ["et"] = nameEt
                },
                Location = location,
                Status = status,
                PricePerKwh = pricePerKwh,
                MaxPower = maxPower,
                IsActive = true,
                CompanyId = companyId
            };
            context.ChargingStations.Add(station);
            await context.SaveChangesAsync(ct);
            return station;
        }

        if (station.CompanyId == null)
        {
            station.CompanyId = companyId;
            await context.SaveChangesAsync(ct);
        }

        return station;
    }

    private static async Task AddStationConnectorIfMissingAsync(
        ChargingDbContext context,
        Guid stationId,
        Guid connectorId,
        CancellationToken ct)
    {
        var exists = await context.ChargingStationConnectors
            .AnyAsync(sc => sc.ChargingStationId == stationId && sc.ConnectorId == connectorId, ct);

        if (exists)
        {
            return;
        }

        context.ChargingStationConnectors.Add(new ChargingStationConnector
        {
            Id = Guid.NewGuid(),
            ChargingStationId = stationId,
            ConnectorId = connectorId
        });
        await context.SaveChangesAsync(ct);
    }

    private static bool MatchLocalizedName(LangStr? name, string expectedEn)
    {
        if (name == null || name.Count == 0)
        {
            return false;
        }

        return name.TryGetValue("en", out var en)
               && string.Equals(en, expectedEn, StringComparison.OrdinalIgnoreCase);
    }
}
