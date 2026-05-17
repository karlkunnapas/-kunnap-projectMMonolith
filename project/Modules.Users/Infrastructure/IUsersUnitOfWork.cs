namespace Modules.Users.Infrastructure;

internal interface IUsersUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
