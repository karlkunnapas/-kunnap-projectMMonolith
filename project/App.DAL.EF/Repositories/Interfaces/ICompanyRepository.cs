using App.Domain;

namespace App.DAL.EF.Repositories.Interfaces;

/// <summary>
/// Specialized repository for Company entities.
/// Provides system-level methods that bypass tenant filters for admin operations.
/// </summary>
public interface ICompanyRepository : IRepository<Company>
{
    /// <summary>
    /// Gets a company by its slug, bypassing the global query filters.
    /// Used by TenantResolutionMiddleware to resolve companies before tenant context is established.
    /// This method returns inactive/deleted companies as well - caller must check IsActive.
    /// </summary>
    Task<Company?> GetBySlugIgnoringFiltersAsync(string slug);

    /// <summary>
    /// Gets all companies across all tenants, bypassing the global query filters.
    /// Used by SystemAdmin for cross-tenant company management.
    /// This includes inactive companies.
    /// </summary>
    Task<IEnumerable<Company>> GetAllIgnoringFiltersAsync();

    /// <summary>
    /// Gets a company by ID, bypassing the global query filters.
    /// Used for system-level operations that need to access deactivated companies.
    /// This includes inactive companies.
    /// </summary>
    Task<Company?> GetByIdIgnoringFiltersAsync(Guid id);

    /// <summary>
    /// Reactivates a previously deactivated company by clearing IsDeleted.
    /// This bypasses the soft delete query filter to find the deactivated record.
    /// </summary>
    Task ReactivateAsync(Guid id);
}