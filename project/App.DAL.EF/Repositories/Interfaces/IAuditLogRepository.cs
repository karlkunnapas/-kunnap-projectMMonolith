using App.Domain;

namespace App.DAL.EF.Repositories.Interfaces;

public interface IAuditLogRepository
{
    Task<List<AuditLog>> GetByEntityAsync(string entityName, Guid entityId, Guid? companyId = null);
    Task<List<AuditLog>> GetByCompanyAsync(
        Guid companyId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null);
    Task<List<AuditLog>> GetRangeAsync(DateTime? fromUtc = null, DateTime? toUtc = null);
    Task<List<AuditLog>> GetSystemAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null,
        string? actor = null,
        Guid? entityId = null,
        int page = 1,
        int pageSize = 50);
    Task<int> GetSystemCountAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null,
        string? actor = null,
        Guid? entityId = null);
}
