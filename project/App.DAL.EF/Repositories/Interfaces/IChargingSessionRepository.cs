using App.Domain;

namespace App.DAL.EF.Repositories.Interfaces;

public interface IChargingSessionRepository
{
    Task<ChargingSession?> GetByIdAsync(Guid id);
    Task<ChargingSession?> GetByIdForUserAsync(Guid id, Guid userId);
    Task<ChargingSession?> GetByReservationIdAsync(Guid reservationId);
    Task<List<ChargingSession>> GetByUserIdAsync(Guid userId);
    Task<List<ChargingSession>> GetActiveSessionsByStationAsync(Guid stationId);
    Task<List<ChargingSession>> GetByStationAndRangeAsync(Guid stationId, DateTime fromUtc, DateTime toUtc);
    Task AddAsync(ChargingSession session);
    void Update(ChargingSession session);
}
