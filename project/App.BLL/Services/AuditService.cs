using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using Mediator;
using Shared.Contracts.Companies;
using Shared.Contracts.Companies.Events;

namespace App.BLL.Services;

public class AuditService : IAuditService
{
    private readonly IMediator _mediator;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public AuditService(IMediator mediator, ICompaniesModuleApi companiesModuleApi)
    {
        _mediator = mediator;
        _companiesModuleApi = companiesModuleApi;
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

        var trail = await _companiesModuleApi.GetAuditTrailAsync(entityName, entityId, companyId);
        var entries = trail.Entries
            .OrderBy(x => x.AtUtc)
            .Select(MapEntry)
            .ToList();

        var dto = BllDtoFactory.CreateAuditTrailDto(entityName, entityId, entries);

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

        var logs = await _companiesModuleApi.GetCompanyAuditAsync(companyId, fromUtc, toUtc, entityName, action);
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

        await _mediator.Publish(new AuditLogMutationRequestedNotification
        {
            CompanyId = companyId,
            UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim(),
            EntityName = entityName.Trim(),
            EntityId = entityId,
            Action = action.Trim(),
            AtUtc = DateTime.UtcNow,
            ChangesJson = changesJson
        });
        return ServiceResult.Ok();
    }

    private static AuditEntryDto MapEntry(CompanyAuditEntryContract entry)
    {
        return new AuditEntryDto
        {
            Id = entry.Id,
            UserName = entry.UserName,
            EntityName = entry.EntityName,
            EntityId = entry.EntityId,
            Action = entry.Action,
            AtUtc = entry.AtUtc,
            ChangesJson = entry.ChangesJson
        };
    }
}
