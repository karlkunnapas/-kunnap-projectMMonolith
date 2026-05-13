namespace Modules.Charging.Infrastructure;

internal sealed class ChargingUnitOfWork : IChargingUnitOfWork
{
    private readonly ChargingDbContext _dbContext;

    public ChargingUnitOfWork(ChargingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _dbContext.SaveChangesAsync(ct);
    }
}
