using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal interface IReservationRepository
{
    IQueryable<Reservation> Query();
    void Add(Reservation reservation);
}
