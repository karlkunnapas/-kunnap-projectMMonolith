using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<AuditLog>> GetByEntityAsync(string entityName, Guid entityId, Guid? companyId = null)
    {
        var query = _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == entityName && a.EntityId == entityId);

        if (companyId.HasValue)
        {
            query = query.Where(a => a.CompanyId == companyId.Value);
        }

        return query
            .OrderBy(a => a.AtUtc)
            .ToListAsync();
    }

    public Task<List<AuditLog>> GetByCompanyAsync(
        Guid companyId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null)
    {
        var query = _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.CompanyId == companyId);

        if (fromUtc.HasValue)
        {
            query = query.Where(a => a.AtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(a => a.AtUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(a => a.EntityName == entityName);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        return query
            .OrderByDescending(a => a.AtUtc)
            .ToListAsync();
    }

    public Task<List<AuditLog>> GetRangeAsync(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var query = _context.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(a => a.AtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(a => a.AtUtc <= toUtc.Value);
        }

        return query
            .OrderByDescending(a => a.AtUtc)
            .ToListAsync();
    }

    public Task<List<AuditLog>> GetSystemAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null,
        string? actor = null,
        Guid? entityId = null,
        int page = 1,
        int pageSize = 50)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 50 : pageSize;

        return BuildSystemQuery(fromUtc, toUtc, entityName, action, actor, entityId)
            .OrderByDescending(a => a.AtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> GetSystemCountAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null,
        string? actor = null,
        Guid? entityId = null)
    {
        return BuildSystemQuery(fromUtc, toUtc, entityName, action, actor, entityId).CountAsync();
    }

    private IQueryable<AuditLog> BuildSystemQuery(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? entityName,
        string? action,
        string? actor,
        Guid? entityId)
    {
        var query = _context.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(a => a.AtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(a => a.AtUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var normalized = entityName.Trim();
            query = query.Where(a => a.EntityName == normalized);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalized = action.Trim();
            query = query.Where(a => a.Action == normalized);
        }

        if (!string.IsNullOrWhiteSpace(actor))
        {
            var normalized = actor.Trim().ToLower();
            query = query.Where(a => a.UserName.ToLower().Contains(normalized));
        }

        if (entityId.HasValue)
        {
            query = query.Where(a => a.EntityId == entityId.Value);
        }

        return query;
    }
}
