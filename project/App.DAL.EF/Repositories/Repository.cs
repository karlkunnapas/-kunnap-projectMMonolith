using System.Linq.Expressions;
using App.DAL.EF.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories.Implementations;

/// <summary>
/// Generic repository implementation that wraps ApplicationDbContext.
/// IMPORTANT: This repository does NOT apply CompanyId filters - the DbContext's global query filter handles tenant scoping automatically.
/// IMPORTANT: This repository does NOT set IsDeleted - the DbContext's ApplyAuditAndSoftDelete() handles soft delete automatically when Remove() is called.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    /// <summary>
    /// Gets an entity by its ID. The global query filter ensures only current tenant's non-deleted records are returned.
    /// </summary>
    public virtual async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    /// <summary>
    /// Gets all entities for the current tenant. The global query filter handles tenant scoping and soft-delete filtering.
    /// </summary>
    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    /// <summary>
    /// Finds entities matching the predicate. The global query filter is automatically applied.
    /// </summary>
    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    /// <summary>
    /// Gets a queryable for the current tenant. The global query filter is automatically applied.
    /// </summary>
    public virtual IQueryable<T> GetQueryable()
    {
        return _dbSet.AsQueryable();
    }

    /// <summary>
    /// Adds a new entity. The DbContext automatically stamps CompanyId, CreatedAtUtc, CreatedByUserId, and IsDeleted.
    /// </summary>
    public virtual async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    /// <summary>
    /// Updates an entity. The DbContext automatically stamps UpdatedAtUtc and UpdatedByUserId.
    /// </summary>
    public virtual void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    /// <summary>
    /// Removes an entity. The DbContext automatically converts this to a soft delete.
    /// NEVER set IsDeleted manually - always use this method.
    /// </summary>
    public virtual void Remove(T entity)
    {
        _dbSet.Remove(entity);
        // The DbContext's ApplyAuditAndSoftDelete() will intercept this and set IsDeleted = true
    }
}