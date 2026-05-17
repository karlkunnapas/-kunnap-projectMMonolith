namespace Modules.Companies.Infrastructure;

internal interface ICompaniesUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
