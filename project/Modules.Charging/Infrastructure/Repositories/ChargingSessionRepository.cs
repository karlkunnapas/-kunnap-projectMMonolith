using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal sealed class ChargingSessionRepository : IChargingSessionRepository
{
    private readonly ChargingDbContext _dbContext;

    public ChargingSessionRepository(ChargingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<ChargingSession> Query() => _dbContext.ChargingSessions;

    public void Add(ChargingSession session) => _dbContext.ChargingSessions.Add(session);
}
