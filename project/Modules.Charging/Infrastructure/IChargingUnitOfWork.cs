namespace Modules.Charging.Infrastructure;

internal interface IChargingUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
