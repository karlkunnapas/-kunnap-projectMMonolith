using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;

namespace App.BLL.Services;

public class AuditService : IAuditService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuditService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<AuditTrailDto>> GetAuditTrailAsync(string entityName, Guid entityId, Guid? companyId)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            return ServiceResult<AuditTrailDto>.Fail("VALIDATION", "Entity name is required.");
        }

        if (entityId == Guid.Empty)
        {
            return ServiceResult<AuditTrailDto>.Fail("VALIDATION", "Entity id is required.");
        }

        var entries = await _unitOfWork.AuditLogQueries.GetByEntityAsync(entityName, entityId, companyId);

        var dto = new AuditTrailDto
        {
            EntityName = entityName,
            EntityId = entityId,
            Entries = entries
                .OrderBy(e => e.AtUtc)
                .Select(MapEntry)
                .ToList()
        };

        return ServiceResult<AuditTrailDto>.Ok(dto);
    }

    public async Task<ServiceResult<List<AuditEntryDto>>> GetCompanyAuditAsync(
        Guid companyId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<AuditEntryDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var logs = await _unitOfWork.AuditLogQueries.GetByCompanyAsync(companyId, fromUtc, toUtc, entityName, action);
        return ServiceResult<List<AuditEntryDto>>.Ok(logs.Select(MapEntry).ToList());
    }

    private static AuditEntryDto MapEntry(App.Domain.AuditLog entry)
    {
        return new AuditEntryDto
        {
            Id = entry.Id,
            Action = entry.Action,
            UserName = entry.UserName,
            EntityName = entry.EntityName,
            EntityId = entry.EntityId,
            AtUtc = entry.AtUtc,
            ChangesJson = entry.ChangesJson
        };
    }
}

