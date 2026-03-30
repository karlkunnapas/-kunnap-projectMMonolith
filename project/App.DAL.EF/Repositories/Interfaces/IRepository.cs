using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories.Interfaces;

/// <summary>
/// Generic repository interface for basic CRUD operations.
/// The ApplicationDbContext's global query filter automatically applies tenant scoping and soft-delete filtering.
/// Repositories must NOT add manual CompanyId where-clauses - the DbContext handles this automatically.
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Gets an entity by its ID. Returns null if not found or if the entity belongs to another tenant.
    /// The global query filter automatically scopes to the current tenant.
    /// </summary>
    Task<T?> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets all entities for the current tenant (filtered by global query filter).
    /// Excludes soft-deleted records automatically.
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Finds entities matching the predicate. Scoped to current tenant via global query filter.
    /// </summary>
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Gets a queryable for the current tenant. Use for complex queries.
    /// The global query filter is automatically applied.
    /// </summary>
    IQueryable<T> GetQueryable();

    /// <summary>
    /// Adds a new entity. The DbContext automatically sets:
    /// - CompanyId (from ITenantContext)
    /// - CreatedAtUtc
    /// - CreatedByUserId
    /// - IsDeleted = false
    /// </summary>
    Task AddAsync(T entity);

    /// <summary>
    /// Updates an entity. The DbContext automatically sets UpdatedAtUtc and UpdatedByUserId.
    /// </summary>
    void Update(T entity);

    /// <summary>
    /// Removes an entity. The DbContext automatically converts this to a soft delete
    /// by setting IsDeleted = true and DeletedAtUtc.
    /// NEVER set IsDeleted manually - always call this method.
    /// </summary>
    void Remove(T entity);
}