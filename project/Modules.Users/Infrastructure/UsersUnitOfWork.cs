namespace Modules.Users.Infrastructure;

internal sealed class UsersUnitOfWork : IUsersUnitOfWork
{
    private readonly UsersDbContext _dbContext;

    public UsersUnitOfWork(UsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _dbContext.SaveChangesAsync(ct);
    }
}
