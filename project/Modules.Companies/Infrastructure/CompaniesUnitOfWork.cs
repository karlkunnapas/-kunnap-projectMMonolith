namespace Modules.Companies.Infrastructure;

internal sealed class CompaniesUnitOfWork : ICompaniesUnitOfWork
{
    private readonly CompaniesDbContext _dbContext;

    public CompaniesUnitOfWork(CompaniesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _dbContext.SaveChangesAsync(ct);
    }
}
