using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories.Implementations;

/// <summary>
/// Specialized repository for Company entities.
/// Provides system-level methods that bypass tenant filters for admin operations.
/// </summary>
public class CompanyRepository : Repository<Company>, ICompanyRepository
{
    public CompanyRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Gets a company by its slug, bypassing the global query filters.
    /// Used by TenantResolutionMiddleware to resolve companies before tenant context is established.
    /// This method returns inactive/deleted companies as well - caller must check IsActive.
    /// </summary>
    public async Task<Company?> GetBySlugIgnoringFiltersAsync(string slug)
    {
        return await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Slug == slug);
    }

    /// <summary>
    /// Gets all companies across all tenants, bypassing the global query filters.
    /// Used by SystemAdmin for cross-tenant company management.
    /// This includes inactive companies.
    /// </summary>
    public async Task<IEnumerable<Company>> GetAllIgnoringFiltersAsync()
    {
        return await _dbSet
            .IgnoreQueryFilters()
            .ToListAsync();
    }

    /// <summary>
    /// Gets a company by ID, bypassing the global query filters.
    /// Used for system-level operations that need to access deactivated companies.
    /// This includes inactive companies.
    /// </summary>
    public async Task<Company?> GetByIdIgnoringFiltersAsync(Guid id)
    {
        return await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>
    /// Reactivates a previously deactivated company by clearing IsDeleted.
    /// This bypasses the soft delete query filter to find the deactivated record.
    /// The DbContext will handle setting UpdatedAtUtc automatically.
    /// </summary>
    public async Task ReactivateAsync(Guid id)
    {
        var company = await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (company != null && !company.IsActive)
        {
            company.IsActive = true;
            // Note: IsDeleted is not on Company entity, IsActive is the flag
            // We use IsActive for the active/deactivated state
            _dbSet.Update(company);
        }
    }
}