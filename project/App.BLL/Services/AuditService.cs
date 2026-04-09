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

    public async Task<ServiceResult> LogMutationAsync(
        Guid companyId,
        string userName,
        string entityName,
        Guid entityId,
        string action,
        string? changesJson = null)
    {
        if (companyId == Guid.Empty || entityId == Guid.Empty)
        {
            return ServiceResult.Fail("VALIDATION", "Company and entity identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(action))
        {
            return ServiceResult.Fail("VALIDATION", "Entity name and action are required.");
        }

        await _unitOfWork.AuditLogs.AddAsync(new App.Domain.AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim(),
            EntityName = entityName.Trim(),
            EntityId = entityId,
            Action = action.Trim(),
            AtUtc = DateTime.UtcNow,
            ChangesJson = changesJson
        });

        await _unitOfWork.SaveAsync();
        return ServiceResult.Ok();
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
