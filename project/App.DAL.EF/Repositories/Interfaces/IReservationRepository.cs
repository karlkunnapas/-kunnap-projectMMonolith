using App.Domain;

namespace App.DAL.EF.Repositories.Interfaces;

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid id);
    Task<Reservation?> GetByIdForUserAsync(Guid id, Guid userId);
    Task<List<Reservation>> GetByUserIdAsync(Guid userId);
    Task<List<Reservation>> GetByStationIdAsync(Guid stationId);
    Task<List<Reservation>> GetActiveReservationsByStationAsync(Guid stationId);
    Task<List<Reservation>> GetOverlappingReservationsAsync(Guid stationId, DateTime startTimeUtc, DateTime endTimeUtc, Guid? excludeReservationId = null);
    Task AddAsync(Reservation reservation);
    void Update(Reservation reservation);
}

