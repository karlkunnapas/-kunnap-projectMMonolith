using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Modules.Charging.Infrastructure;
using Shared.Contracts.Charging;
using DomainStation = Modules.Charging.Domain.ChargingStation;

namespace Modules.Charging.Application;

internal sealed class ChargingModuleApi : IChargingModuleApi
{
    private readonly ChargingDbContext _dbContext;

    public ChargingModuleApi(ChargingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ChargingStationContract>> GetStationsForHomeAsync(EStationStatus? status = null, CancellationToken ct = default)
    {
        var query = _dbContext.ChargingStations
            .AsNoTracking()
            .Where(s => s.IsActive);

        if (status.HasValue)
        {
            var moduleStatus = MapStationStatus(status.Value);
            query = query.Where(s => s.Status == moduleStatus);
        }

        var stations = await query
            .Include(s => s.ChargingStationConnectors!)
                .ThenInclude(link => link.Connector)
            .OrderBy(s => s.Location)
            .ToListAsync(ct);

        return stations.Select(MapStation).ToList();
    }

    public async Task<IReadOnlyCollection<AdminChargingStationContract>> GetStationsForAdminAsync(CancellationToken ct = default)
    {
        var stations = await _dbContext.ChargingStations
            .AsNoTracking()
            .OrderBy(s => s.NameJson)
            .ThenBy(s => s.Location)
            .ToListAsync(ct);

        return stations.Select(station =>
        {
            var names = ExtractLocalizedNames(station.NameJson);
            return new AdminChargingStationContract
            {
                StationId = station.Id,
                CompanyId = station.CompanyId,
                NameEn = names.en,
                NameEt = names.et,
                Location = station.Location,
                Status = MapStationStatus(station.Status),
                IsActive = station.IsActive,
                PricePerKwh = station.PricePerKwh,
                MaxPower = station.MaxPower
            };
        }).ToList();
    }

    public async Task<ChargingStationContract?> GetStationByIdAsync(Guid stationId, CancellationToken ct = default)
    {
        var station = await _dbContext.ChargingStations
            .AsNoTracking()
            .Include(s => s.ChargingStationConnectors!)
                .ThenInclude(link => link.Connector)
            .FirstOrDefaultAsync(s => s.Id == stationId, ct);

        return station == null ? null : MapStation(station);
    }

    public Task<Guid?> GetStationCompanyIdAsync(Guid stationId, CancellationToken ct = default)
    {
        return _dbContext.ChargingStations
            .AsNoTracking()
            .Where(s => s.Id == stationId)
            .Select(s => s.CompanyId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyCollection<Guid>> GetStationConnectorIdsAsync(Guid stationId, CancellationToken ct = default)
    {
        return await _dbContext.ChargingStationConnectors
            .AsNoTracking()
            .Where(link => link.ChargingStationId == stationId)
            .Select(link => link.ConnectorId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<ChargingStationContract>> GetCompanyStationsAsync(Guid companyId, CancellationToken ct = default)
    {
        var stations = await _dbContext.ChargingStations
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .Include(s => s.ChargingStationConnectors!)
                .ThenInclude(link => link.Connector)
            .OrderBy(s => s.Location)
            .ToListAsync(ct);

        return stations.Select(MapStation).ToList();
    }

    public async Task<ChargingStationContract?> GetCompanyStationByIdAsync(Guid stationId, Guid companyId, CancellationToken ct = default)
    {
        var station = await _dbContext.ChargingStations
            .AsNoTracking()
            .Include(s => s.ChargingStationConnectors!)
                .ThenInclude(link => link.Connector)
            .FirstOrDefaultAsync(s => s.Id == stationId && s.CompanyId == companyId, ct);
        return station == null ? null : MapStation(station);
    }

    public async Task<CompanyDashboardStatsContract> GetCompanyDashboardStatsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var stationsQuery = _dbContext.ChargingStations
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId);

        var totalStations = await stationsQuery.CountAsync(ct);
        var availableStations = await stationsQuery.CountAsync(s => s.Status == Domain.EStationStatus.Available, ct);
        var inUseStations = await stationsQuery.CountAsync(s => s.Status == Domain.EStationStatus.InUse, ct);
        var maintenanceStations = await stationsQuery.CountAsync(s => s.Status == Domain.EStationStatus.Maintenance, ct);

        var stationIds = await stationsQuery.Select(s => s.Id).ToListAsync(ct);

        var activeReservations = await _dbContext.Reservations
            .AsNoTracking()
            .CountAsync(r => stationIds.Contains(r.ChargingStationId)
                             && r.Status == Domain.EReservationStatus.Active
                             && r.StartTime <= toUtc
                             && r.EndTime >= fromUtc, ct);

        var activeSessions = await _dbContext.ChargingSessions
            .AsNoTracking()
            .CountAsync(s => stationIds.Contains(s.ChargingStationId)
                             && s.StartTime <= toUtc
                             && (s.EndTime == null || s.EndTime >= fromUtc), ct);

        var revenueTotal = await _dbContext.ChargingSessions
            .AsNoTracking()
            .Where(s => stationIds.Contains(s.ChargingStationId)
                        && s.EndTime != null
                        && s.EndTime >= fromUtc
                        && s.EndTime <= toUtc)
            .SumAsync(s => (decimal?)s.Cost, ct) ?? 0m;

        return new CompanyDashboardStatsContract
        {
            TotalStations = totalStations,
            AvailableStations = availableStations,
            InUseStations = inUseStations,
            MaintenanceStations = maintenanceStations,
            ActiveReservations = activeReservations,
            ActiveSessions = activeSessions,
            RevenueTotal = revenueTotal
        };
    }

    public async Task<IReadOnlyCollection<ChargingSessionContract>> GetCompanyChargingSessionsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var stationIds = await _dbContext.ChargingStations
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var sessions = await _dbContext.ChargingSessions
            .AsNoTracking()
            .Include(s => s.ChargingStation)
            .Where(s => stationIds.Contains(s.ChargingStationId)
                        && s.StartTime <= toUtc
                        && (s.EndTime == null || s.EndTime >= fromUtc))
            .ToListAsync(ct);

        return sessions.Select(MapSession).ToList();
    }

    public async Task<IReadOnlyCollection<ReservationContract>> GetCompanyReservationsAsync(Guid companyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var stationIds = await _dbContext.ChargingStations
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var reservations = await _dbContext.Reservations
            .AsNoTracking()
            .Include(r => r.ChargingStation)
            .Where(r => stationIds.Contains(r.ChargingStationId)
                        && r.StartTime <= toUtc
                        && r.EndTime >= fromUtc)
            .ToListAsync(ct);

        return reservations.Select(MapReservation).ToList();
    }

    public Task<int> GetReservationCountByRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        return _dbContext.Reservations
            .AsNoTracking()
            .CountAsync(r => r.StartTime <= toUtc && r.EndTime >= fromUtc, ct);
    }

    public async Task<IReadOnlyCollection<ConnectorContract>> GetConnectorsAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _dbContext.Connectors.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var connectors = await query
            .OrderBy(c => c.NameJson)
            .ToListAsync(ct);

        return connectors.Select(c => new ConnectorContract
        {
            Id = c.Id,
            Name = ExtractDisplayName(c.NameJson),
            IsActive = c.IsActive
        }).ToList();
    }

    public async Task<ConnectorTypeContract?> GetConnectorTypeByIdAsync(Guid connectorTypeId, CancellationToken ct = default)
    {
        var connector = await _dbContext.Connectors
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connectorTypeId, ct);
        return connector == null ? null : MapConnectorType(connector);
    }

    public async Task<ConnectorTypeContract> CreateConnectorTypeAsync(string nameEn, string nameEt, bool isActive, CancellationToken ct = default)
    {
        var connector = new Domain.Connector
        {
            Id = Guid.NewGuid(),
            NameJson = BuildNameJson(nameEn, nameEt),
            IsActive = isActive
        };

        _dbContext.Connectors.Add(connector);
        await _dbContext.SaveChangesAsync(ct);
        return MapConnectorType(connector);
    }

    public async Task<ConnectorTypeContract?> UpdateConnectorTypeAsync(Guid connectorTypeId, string nameEn, string nameEt, bool isActive, CancellationToken ct = default)
    {
        var connector = await _dbContext.Connectors
            .FirstOrDefaultAsync(c => c.Id == connectorTypeId, ct);
        if (connector == null)
        {
            return null;
        }

        connector.NameJson = BuildNameJson(nameEn, nameEt);
        connector.IsActive = isActive;
        await _dbContext.SaveChangesAsync(ct);
        return MapConnectorType(connector);
    }

    public async Task<bool> DeleteConnectorTypeAsync(Guid connectorTypeId, CancellationToken ct = default)
    {
        var connector = await _dbContext.Connectors
            .FirstOrDefaultAsync(c => c.Id == connectorTypeId, ct);
        if (connector == null)
        {
            return false;
        }

        _dbContext.Connectors.Remove(connector);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ChargingStationContract> CreateCompanyStationAsync(UpsertCompanyStationContract request, CancellationToken ct = default)
    {
        var station = new DomainStation
        {
            Id = request.StationId ?? Guid.NewGuid(),
            CompanyId = request.CompanyId,
            NameJson = BuildNameJson(request.NameEn, request.NameEt),
            Location = request.Location,
            Status = MapStationStatus(request.Status),
            PricePerKwh = request.PricePerKwh,
            MaxPower = request.MaxPower,
            IsActive = request.IsActive
        };

        _dbContext.ChargingStations.Add(station);
        await _dbContext.SaveChangesAsync(ct);
        var created = await _dbContext.ChargingStations.AsNoTracking().FirstAsync(s => s.Id == station.Id, ct);
        return MapStation(created);
    }

    public async Task<ChargingStationContract?> UpdateCompanyStationAsync(UpsertCompanyStationContract request, CancellationToken ct = default)
    {
        if (!request.StationId.HasValue)
        {
            return null;
        }

        var station = await _dbContext.ChargingStations
            .FirstOrDefaultAsync(s => s.Id == request.StationId.Value && s.CompanyId == request.CompanyId, ct);
        if (station == null)
        {
            return null;
        }

        station.NameJson = BuildNameJson(request.NameEn, request.NameEt);
        station.Location = request.Location;
        station.Status = MapStationStatus(request.Status);
        station.PricePerKwh = request.PricePerKwh;
        station.MaxPower = request.MaxPower;
        station.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync(ct);

        var updated = await _dbContext.ChargingStations
            .AsNoTracking()
            .Include(s => s.ChargingStationConnectors!)
                .ThenInclude(link => link.Connector)
            .FirstAsync(s => s.Id == station.Id, ct);
        return MapStation(updated);
    }

    public async Task<bool> DeleteCompanyStationAsync(Guid stationId, Guid companyId, CancellationToken ct = default)
    {
        var station = await _dbContext.ChargingStations
            .Include(s => s.Reservations)
            .Include(s => s.ChargingSessions)
            .Include(s => s.MaintenanceIssues)
            .FirstOrDefaultAsync(s => s.Id == stationId && s.CompanyId == companyId, ct);
        if (station == null)
        {
            return false;
        }

        if ((station.Reservations?.Any() ?? false) || (station.ChargingSessions?.Any() ?? false) || (station.MaintenanceIssues?.Any() ?? false))
        {
            return false;
        }

        var links = await _dbContext.ChargingStationConnectors
            .Where(link => link.ChargingStationId == stationId)
            .ToListAsync(ct);
        _dbContext.ChargingStationConnectors.RemoveRange(links);
        _dbContext.ChargingStations.Remove(station);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyCollection<Guid>> GetStationAssignedConnectorIdsAsync(Guid stationId, CancellationToken ct = default)
    {
        return await _dbContext.ChargingStationConnectors
            .AsNoTracking()
            .Where(x => x.ChargingStationId == stationId)
            .Select(x => x.ConnectorId)
            .ToListAsync(ct);
    }

    public async Task SetStationConnectorsAsync(Guid stationId, IReadOnlyCollection<Guid> connectorIds, CancellationToken ct = default)
    {
        var normalized = connectorIds.Where(id => id != Guid.Empty).Distinct().ToHashSet();
        var current = await _dbContext.ChargingStationConnectors
            .Where(x => x.ChargingStationId == stationId)
            .ToListAsync(ct);

        var remove = current.Where(x => !normalized.Contains(x.ConnectorId)).ToList();
        if (remove.Count > 0)
        {
            _dbContext.ChargingStationConnectors.RemoveRange(remove);
        }

        var existing = current.Select(x => x.ConnectorId).ToHashSet();
        foreach (var connectorId in normalized.Where(id => !existing.Contains(id)))
        {
            _dbContext.ChargingStationConnectors.Add(new Domain.ChargingStationConnector
            {
                Id = Guid.NewGuid(),
                ChargingStationId = stationId,
                ConnectorId = connectorId
            });
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<ReservationContract>> GetOverlappingReservationsAsync(
        Guid stationId,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        Guid? excludeReservationId = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.Reservations
            .AsNoTracking()
            .Where(r => r.ChargingStationId == stationId
                        && r.StartTime < endTimeUtc
                        && r.EndTime > startTimeUtc
                        && r.Status != Domain.EReservationStatus.Cancelled
                        && r.Status != Domain.EReservationStatus.Expired);

        if (excludeReservationId.HasValue && excludeReservationId.Value != Guid.Empty)
        {
            query = query.Where(r => r.Id != excludeReservationId.Value);
        }

        var reservations = await query.ToListAsync(ct);
        return reservations.Select(MapReservation).ToList();
    }

    public async Task<IReadOnlyCollection<ReservationContract>> GetStationReservationsAsync(Guid stationId, CancellationToken ct = default)
    {
        var reservations = await _dbContext.Reservations
            .AsNoTracking()
            .Where(r => r.ChargingStationId == stationId)
            .OrderBy(r => r.StartTime)
            .ToListAsync(ct);

        return reservations.Select(MapReservation).ToList();
    }

    public async Task<IReadOnlyCollection<ReservationContract>> GetUserReservationsAsync(Guid userId, CancellationToken ct = default)
    {
        var reservations = await _dbContext.Reservations
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.StartTime)
            .ToListAsync(ct);

        return reservations.Select(MapReservation).ToList();
    }

    public async Task<ReservationContract?> GetReservationByIdForUserAsync(Guid reservationId, Guid userId, CancellationToken ct = default)
    {
        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .Include(r => r.ChargingStation)
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId, ct);

        return reservation == null ? null : MapReservation(reservation);
    }

    public async Task<ReservationContract?> GetReservationByIdAsync(Guid reservationId, CancellationToken ct = default)
    {
        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .Include(r => r.ChargingStation)
            .FirstOrDefaultAsync(r => r.Id == reservationId, ct);

        return reservation == null ? null : MapReservation(reservation);
    }

    public async Task<ReservationContract> CreateReservationAsync(ReservationContract reservation, CancellationToken ct = default)
    {
        var entity = new Domain.Reservation
        {
            Id = reservation.Id,
            UserId = reservation.UserId,
            ChargingStationId = reservation.ChargingStationId,
            StartTime = reservation.StartTimeUtc,
            EndTime = reservation.EndTimeUtc,
            ExpiresAtUtc = reservation.ExpiresAtUtc,
            CancelledAtUtc = reservation.CancelledAtUtc,
            EstimatedCost = reservation.EstimatedCost,
            Status = MapReservationStatus(reservation.Status),
            PromotionId = reservation.PromotionId
        };

        _dbContext.Reservations.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        var created = await _dbContext.Reservations
            .AsNoTracking()
            .Include(r => r.ChargingStation)
            .FirstAsync(r => r.Id == entity.Id, ct);
        return MapReservation(created);
    }

    public async Task<bool> UpdateReservationStatusAsync(
        Guid reservationId,
        EReservationStatus status,
        DateTime? expiresAtUtc = null,
        DateTime? cancelledAtUtc = null,
        EStationStatus? stationStatus = null,
        CancellationToken ct = default)
    {
        var reservation = await _dbContext.Reservations
            .Include(r => r.ChargingStation)
            .FirstOrDefaultAsync(r => r.Id == reservationId, ct);
        if (reservation == null)
        {
            return false;
        }

        reservation.Status = MapReservationStatus(status);
        if (expiresAtUtc.HasValue)
        {
            reservation.ExpiresAtUtc = expiresAtUtc.Value;
        }

        reservation.CancelledAtUtc = cancelledAtUtc;

        if (stationStatus.HasValue && reservation.ChargingStation != null)
        {
            reservation.ChargingStation.Status = MapStationStatus(stationStatus.Value);
        }

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ChargingSessionContract?> GetChargingSessionByIdForUserAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var session = await _dbContext.ChargingSessions
            .AsNoTracking()
            .Include(s => s.ChargingStation)
            .Include(s => s.Reservation)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);

        return session == null ? null : MapSession(session);
    }

    public async Task<ChargingSessionContract?> GetChargingSessionByIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _dbContext.ChargingSessions
            .AsNoTracking()
            .Include(s => s.ChargingStation)
            .Include(s => s.Reservation)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        return session == null ? null : MapSession(session);
    }

    public async Task<IReadOnlyCollection<ChargingSessionContract>> GetUserChargingSessionsAsync(Guid userId, CancellationToken ct = default)
    {
        var sessions = await _dbContext.ChargingSessions
            .AsNoTracking()
            .Include(s => s.ChargingStation)
            .Include(s => s.Reservation)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync(ct);

        return sessions.Select(MapSession).ToList();
    }

    public async Task<ChargingSessionContract?> GetChargingSessionByReservationIdAsync(Guid reservationId, CancellationToken ct = default)
    {
        var session = await _dbContext.ChargingSessions
            .AsNoTracking()
            .Include(s => s.ChargingStation)
            .Include(s => s.Reservation)
            .FirstOrDefaultAsync(s => s.ReservationId == reservationId, ct);

        return session == null ? null : MapSession(session);
    }

    public async Task<ChargingSessionContract> CreateChargingSessionAsync(ChargingSessionContract session, CancellationToken ct = default)
    {
        var entity = new Domain.ChargingSession
        {
            Id = session.Id,
            UserId = session.UserId,
            ChargingStationId = session.ChargingStationId,
            ReservationId = session.ReservationId,
            PromotionId = session.PromotionId,
            StartTime = session.StartTimeUtc,
            EndTime = session.EndTimeUtc,
            EnergyConsumed = session.EnergyConsumed,
            Cost = session.Cost
        };

        _dbContext.ChargingSessions.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        var created = await _dbContext.ChargingSessions
            .AsNoTracking()
            .Include(s => s.ChargingStation)
            .Include(s => s.Reservation)
            .FirstAsync(s => s.Id == entity.Id, ct);
        return MapSession(created);
    }

    public async Task<bool> CompleteChargingSessionAsync(
        Guid sessionId,
        DateTime endTimeUtc,
        decimal energyConsumed,
        decimal cost,
        Guid? promotionId,
        EStationStatus stationStatus,
        CancellationToken ct = default)
    {
        var session = await _dbContext.ChargingSessions
            .Include(s => s.ChargingStation)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null)
        {
            return false;
        }

        session.EndTime = endTimeUtc;
        session.EnergyConsumed = energyConsumed;
        session.Cost = cost;
        session.PromotionId = promotionId;

        if (session.ChargingStation != null)
        {
            session.ChargingStation.Status = MapStationStatus(stationStatus);
        }

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyCollection<MaintenanceContract>> GetMaintenancesByCompanyAsync(Guid companyId, bool includeResolved, CancellationToken ct = default)
    {
        var query = _dbContext.Maintenances
            .AsNoTracking()
            .Include(m => m.ChargingStation)
            .Where(m => m.ChargingStation != null && m.ChargingStation.CompanyId == companyId);

        if (!includeResolved)
        {
            query = query.Where(m => m.Status != Domain.EMaintenanceStatus.Resolved);
        }

        var items = await query
            .OrderByDescending(m => m.ReportedAt)
            .ToListAsync(ct);
        return items.Select(MapMaintenance).ToList();
    }

    public async Task<MaintenanceContract?> GetMaintenanceByIdForCompanyAsync(Guid maintenanceId, Guid companyId, CancellationToken ct = default)
    {
        var item = await _dbContext.Maintenances
            .AsNoTracking()
            .Include(m => m.ChargingStation)
            .FirstOrDefaultAsync(m => m.Id == maintenanceId && m.ChargingStation != null && m.ChargingStation.CompanyId == companyId, ct);

        return item == null ? null : MapMaintenance(item);
    }

    public async Task<MaintenanceContract> CreateMaintenanceAsync(MaintenanceContract maintenance, CancellationToken ct = default)
    {
        var entity = new Domain.Maintenance
        {
            Id = maintenance.Id,
            ChargingStationId = maintenance.ChargingStationId,
            ReportedByUserId = maintenance.ReportedByUserId,
            IssueDescription = maintenance.IssueDescription,
            Status = MapMaintenanceStatus(maintenance.Status),
            ReportedAt = maintenance.ReportedAtUtc,
            ResolvedAt = maintenance.ResolvedAtUtc,
            AssignedToUserId = maintenance.AssignedToUserId,
            Notes = maintenance.Notes
        };

        _dbContext.Maintenances.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        var created = await _dbContext.Maintenances
            .AsNoTracking()
            .Include(m => m.ChargingStation)
            .FirstAsync(m => m.Id == entity.Id, ct);
        return MapMaintenance(created);
    }

    public async Task<bool> UpdateMaintenanceStatusAsync(Guid maintenanceId, EMaintenanceStatus status, string? notes, DateTime? resolvedAtUtc, CancellationToken ct = default)
    {
        var item = await _dbContext.Maintenances.FirstOrDefaultAsync(m => m.Id == maintenanceId, ct);
        if (item == null)
        {
            return false;
        }

        item.Status = MapMaintenanceStatus(status);
        item.Notes = notes;
        item.ResolvedAt = resolvedAtUtc;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AssignMaintenanceAsync(Guid maintenanceId, Guid? assignedToUserId, CancellationToken ct = default)
    {
        var item = await _dbContext.Maintenances.FirstOrDefaultAsync(m => m.Id == maintenanceId, ct);
        if (item == null)
        {
            return false;
        }

        item.AssignedToUserId = assignedToUserId;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateStationStatusAsync(Guid stationId, EStationStatus status, CancellationToken ct = default)
    {
        var station = await _dbContext.ChargingStations.FirstOrDefaultAsync(s => s.Id == stationId, ct);
        if (station == null)
        {
            return false;
        }

        station.Status = MapStationStatus(status);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    private static ChargingStationContract MapStation(Domain.ChargingStation station)
    {
        return new ChargingStationContract
        {
            Id = station.Id,
            Name = ExtractDisplayName(station.NameJson),
            Location = station.Location,
            Status = MapStationStatus(station.Status),
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            IsActive = station.IsActive,
            CompanyId = station.CompanyId,
            Connectors = (station.ChargingStationConnectors ?? Array.Empty<Domain.ChargingStationConnector>())
                .Where(link => link.Connector != null)
                .Select(link => new ConnectorContract
                {
                    Id = link.ConnectorId,
                    Name = ExtractDisplayName(link.Connector!.NameJson),
                    IsActive = link.Connector!.IsActive
                })
                .ToList()
        };
    }

    private static ReservationContract MapReservation(Domain.Reservation reservation)
    {
        return new ReservationContract
        {
            Id = reservation.Id,
            UserId = reservation.UserId,
            ChargingStationId = reservation.ChargingStationId,
            StartTimeUtc = reservation.StartTime,
            EndTimeUtc = reservation.EndTime,
            ExpiresAtUtc = reservation.ExpiresAtUtc,
            CancelledAtUtc = reservation.CancelledAtUtc,
            EstimatedCost = reservation.EstimatedCost,
            Status = reservation.Status switch
            {
                Domain.EReservationStatus.Active => EReservationStatus.Active,
                Domain.EReservationStatus.Cancelled => EReservationStatus.Cancelled,
                Domain.EReservationStatus.Expired => EReservationStatus.Expired,
                Domain.EReservationStatus.Started => EReservationStatus.Started,
                _ => EReservationStatus.Active
            },
            PromotionId = reservation.PromotionId,
            StationName = ExtractDisplayName(reservation.ChargingStation?.NameJson)
        };
    }

    private static ChargingSessionContract MapSession(Domain.ChargingSession session)
    {
        return new ChargingSessionContract
        {
            Id = session.Id,
            UserId = session.UserId,
            ChargingStationId = session.ChargingStationId,
            ReservationId = session.ReservationId,
            PromotionId = session.PromotionId,
            StartTimeUtc = session.StartTime,
            EndTimeUtc = session.EndTime,
            EnergyConsumed = session.EnergyConsumed,
            Cost = session.Cost,
            StationName = ExtractDisplayName(session.ChargingStation?.NameJson),
            StationPricePerKwh = session.ChargingStation?.PricePerKwh ?? 0m,
            StationMaxPower = session.ChargingStation?.MaxPower,
            PromotionCode = null,
            PromotionDiscountValue = null
        };
    }

    private static MaintenanceContract MapMaintenance(Domain.Maintenance maintenance)
    {
        return new MaintenanceContract
        {
            Id = maintenance.Id,
            CompanyId = maintenance.ChargingStation?.CompanyId,
            ChargingStationId = maintenance.ChargingStationId,
            StationName = ExtractDisplayName(maintenance.ChargingStation?.NameJson),
            ReportedByUserId = maintenance.ReportedByUserId,
            IssueDescription = maintenance.IssueDescription,
            Status = maintenance.Status switch
            {
                Domain.EMaintenanceStatus.Reported => EMaintenanceStatus.Reported,
                Domain.EMaintenanceStatus.InProgress => EMaintenanceStatus.InProgress,
                Domain.EMaintenanceStatus.Resolved => EMaintenanceStatus.Resolved,
                _ => EMaintenanceStatus.Reported
            },
            ReportedAtUtc = maintenance.ReportedAt,
            ResolvedAtUtc = maintenance.ResolvedAt,
            AssignedToUserId = maintenance.AssignedToUserId,
            Notes = maintenance.Notes
        };
    }

    private static string ExtractDisplayName(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (map == null || map.Count == 0)
            {
                return string.Empty;
            }

            if (map.TryGetValue("en", out var english) && !string.IsNullOrWhiteSpace(english))
            {
                return english;
            }

            var first = map.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            return first ?? string.Empty;
        }
        catch
        {
            return json;
        }
    }

    private static (string en, string et) ExtractLocalizedNames(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (string.Empty, string.Empty);
        }

        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (map == null || map.Count == 0)
            {
                return (string.Empty, string.Empty);
            }

            map.TryGetValue("en", out var en);
            map.TryGetValue("et", out var et);
            en ??= string.Empty;
            et ??= string.Empty;
            if (string.IsNullOrWhiteSpace(en))
            {
                en = map.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
            }

            return (en, et);
        }
        catch
        {
            return (json, string.Empty);
        }
    }

    private static ConnectorTypeContract MapConnectorType(Domain.Connector connector)
    {
        var names = ExtractLocalizedNames(connector.NameJson);
        return new ConnectorTypeContract
        {
            ConnectorTypeId = connector.Id,
            NameEn = names.en,
            NameEt = names.et,
            IsActive = connector.IsActive
        };
    }

    private static string BuildNameJson(string nameEn, string nameEt)
    {
        var en = (nameEn ?? string.Empty).Trim();
        var et = (nameEt ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(en))
        {
            en = et;
        }

        if (string.IsNullOrWhiteSpace(et))
        {
            et = en;
        }

        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["en"] = en,
            ["et"] = et
        });
    }

    private static EStationStatus MapStationStatus(Domain.EStationStatus status)
    {
        return status switch
        {
            Domain.EStationStatus.Available => EStationStatus.Available,
            Domain.EStationStatus.InUse => EStationStatus.InUse,
            Domain.EStationStatus.Maintenance => EStationStatus.Maintenance,
            _ => EStationStatus.Available
        };
    }

    private static Domain.EStationStatus MapStationStatus(EStationStatus status)
    {
        return status switch
        {
            EStationStatus.Available => Domain.EStationStatus.Available,
            EStationStatus.InUse => Domain.EStationStatus.InUse,
            EStationStatus.Maintenance => Domain.EStationStatus.Maintenance,
            _ => Domain.EStationStatus.Available
        };
    }

    private static Domain.EReservationStatus MapReservationStatus(EReservationStatus status)
    {
        return status switch
        {
            EReservationStatus.Active => Domain.EReservationStatus.Active,
            EReservationStatus.Cancelled => Domain.EReservationStatus.Cancelled,
            EReservationStatus.Expired => Domain.EReservationStatus.Expired,
            EReservationStatus.Started => Domain.EReservationStatus.Started,
            _ => Domain.EReservationStatus.Active
        };
    }

    private static Domain.EMaintenanceStatus MapMaintenanceStatus(EMaintenanceStatus status)
    {
        return status switch
        {
            EMaintenanceStatus.Reported => Domain.EMaintenanceStatus.Reported,
            EMaintenanceStatus.InProgress => Domain.EMaintenanceStatus.InProgress,
            EMaintenanceStatus.Resolved => Domain.EMaintenanceStatus.Resolved,
            _ => Domain.EMaintenanceStatus.Reported
        };
    }
}
