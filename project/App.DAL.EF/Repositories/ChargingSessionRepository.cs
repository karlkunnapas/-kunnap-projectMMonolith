using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories;

public class ChargingSessionRepository : IChargingSessionRepository
{
    private readonly AppDbContext _context;

    public ChargingSessionRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ChargingSession?> GetByIdAsync(Guid id)
    {
        return _context.ChargingSessions
            .Include(s => s.ChargingStation)
            .Include(s => s.Reservation)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public Task<ChargingSession?> GetByIdForUserAsync(Guid id, Guid userId)
    {
        return _context.ChargingSessions
            .Include(s => s.ChargingStation)
            .Include(s => s.Reservation)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
    }

    public Task<ChargingSession?> GetByReservationIdAsync(Guid reservationId)
    {
        return _context.ChargingSessions
            .Include(s => s.ChargingStation)
            .Include(s => s.Reservation)
            .FirstOrDefaultAsync(s => s.ReservationId == reservationId);
    }

    public Task<List<ChargingSession>> GetByUserIdAsync(Guid userId)
    {
        return _context.ChargingSessions
            .Include(s => s.ChargingStation)
            .Include(s => s.Reservation)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartTime)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<ChargingSession>> GetActiveSessionsByStationAsync(Guid stationId)
    {
        return _context.ChargingSessions
            .Where(s => s.ChargingStationId == stationId && s.EndTime == null)
            .ToListAsync();
    }

    public Task<List<ChargingSession>> GetByStationAndRangeAsync(Guid stationId, DateTime fromUtc, DateTime toUtc)
    {
        return _context.ChargingSessions
            .Include(s => s.ChargingStation)
            .Include(s => s.Reservation)
            .Where(s => s.ChargingStationId == stationId
                        && s.StartTime < toUtc
                        && (s.EndTime == null || s.EndTime > fromUtc))
            .OrderBy(s => s.StartTime)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task AddAsync(ChargingSession session)
    {
        return _context.ChargingSessions.AddAsync(session).AsTask();
    }

    public void Update(ChargingSession session)
    {
        _context.ChargingSessions.Update(session);
    }
}
