using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IAuditService
{
    Task<ServiceResult<AuditTrailDto>> GetAuditTrailAsync(string entityName, Guid entityId, Guid? companyId);
    Task<ServiceResult<List<AuditEntryDto>>> GetCompanyAuditAsync(
        Guid companyId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null);
}
