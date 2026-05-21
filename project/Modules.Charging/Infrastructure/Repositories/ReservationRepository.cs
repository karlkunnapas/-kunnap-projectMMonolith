using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal sealed class ReservationRepository : IReservationRepository
{
    private readonly ChargingDbContext _dbContext;

    public ReservationRepository(ChargingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<Reservation> Query()
    {
        return _dbContext.Reservations;
    }

    public void Add(Reservation reservation)
    {
        _dbContext.Reservations.Add(reservation);
    }
}
