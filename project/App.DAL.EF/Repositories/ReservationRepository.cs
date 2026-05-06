using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly AppDbContext _context;

    public ReservationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Reservation?> GetByIdAsync(Guid id)
    {
        return _context.Reservations
            .Include(r => r.ChargingStation)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public Task<Reservation?> GetByIdForUserAsync(Guid id, Guid userId)
    {
        return _context.Reservations
            .Include(r => r.ChargingStation)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
    }

    public Task<List<Reservation>> GetByUserIdAsync(Guid userId)
    {
        return _context.Reservations
            .Include(r => r.ChargingStation)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.StartTime)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<Reservation>> GetByStationIdAsync(Guid stationId)
    {
        return _context.Reservations
            .Where(r => r.ChargingStationId == stationId)
            .OrderBy(r => r.StartTime)
            .ToListAsync();
    }

    public Task<List<Reservation>> GetActiveReservationsByStationAsync(Guid stationId)
    {
        var nowUtc = DateTime.UtcNow;

        return _context.Reservations
            .Where(r => r.ChargingStationId == stationId
                        && ((r.Status == EReservationStatus.Active
                             && nowUtc <= r.ExpiresAtUtc
                             && r.EndTime > nowUtc)
                            || (r.Status == EReservationStatus.Started
                                && r.EndTime > nowUtc)))
            .ToListAsync();
    }

    public Task<List<Reservation>> GetOverlappingReservationsAsync(Guid stationId, DateTime startTimeUtc, DateTime endTimeUtc, Guid? excludeReservationId = null)
    {
        var nowUtc = DateTime.UtcNow;

        var query = _context.Reservations
            .Where(r => r.ChargingStationId == stationId
                        && ((r.Status == EReservationStatus.Active && nowUtc <= r.ExpiresAtUtc)
                            || r.Status == EReservationStatus.Started)
                        && r.StartTime < endTimeUtc
                        && startTimeUtc < r.EndTime);

        if (excludeReservationId.HasValue)
        {
            query = query.Where(r => r.Id != excludeReservationId.Value);
        }

        return query.ToListAsync();
    }

    public Task<int> GetCountByCompanyAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        return _context.Reservations
            .Where(r => r.ChargingStation != null
                        && r.ChargingStation.CompanyId == companyId
                        && r.StartTime >= fromUtc
                        && r.StartTime <= toUtc)
            .CountAsync();
    }

    public Task<int> GetCountByRangeAsync(DateTime fromUtc, DateTime toUtc)
    {
        return _context.Reservations
            .Where(r => r.StartTime >= fromUtc && r.StartTime <= toUtc)
            .CountAsync();
    }

    public Task<List<Reservation>> GetByCompanyAndRangeAsync(Guid companyId, DateTime fromUtc, DateTime toUtc)
    {
        return _context.Reservations
            .Include(r => r.ChargingStation)
            .Where(r => r.ChargingStation != null
                        && r.ChargingStation.CompanyId == companyId
                        && r.StartTime >= fromUtc
                        && r.StartTime <= toUtc)
            .OrderByDescending(r => r.StartTime)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task AddAsync(Reservation reservation)
    {
        return _context.Reservations.AddAsync(reservation).AsTask();
    }

    public void Update(Reservation reservation)
    {
        _context.Reservations.Update(reservation);

        if (reservation.ChargingStation != null)
        {
            _context.Entry(reservation.ChargingStation).Property(x => x.Status).IsModified = true;
        }
    }
}
