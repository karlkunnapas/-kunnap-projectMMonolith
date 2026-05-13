using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal interface IChargingSessionRepository
{
    IQueryable<ChargingSession> Query();
    void Add(ChargingSession session);
}
