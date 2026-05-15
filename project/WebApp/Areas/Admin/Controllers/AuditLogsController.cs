using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Companies;
using WebApp.Areas.Admin.ViewModels;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root")]
public class AuditLogsController : Controller
{
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public AuditLogsController(ICompaniesModuleApi companiesModuleApi)
    {
        _companiesModuleApi = companiesModuleApi;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? actionFilter = null,
        string? actor = null,
        string? entityId = null,
        int page = 1)
    {
        Guid? entityGuid = null;
        if (!string.IsNullOrWhiteSpace(entityId))
        {
            if (!Guid.TryParse(entityId, out var parsed))
            {
                ModelState.AddModelError(nameof(entityId), App.Resources.Views.Admin.AuditLogs.Index.InvalidEntityId);
            }
            else
            {
                entityGuid = parsed;
            }
        }

        var normalizedFromUtc = NormalizeToUtc(fromUtc);
        var normalizedToUtc = NormalizeToUtc(toUtc);
        var normalizedPage = Math.Max(1, page);
        const int pageSize = 50;

        var companies = await _companiesModuleApi.GetCompaniesForAdminAsync();
        var allEntries = new List<CompanyAuditEntryContract>();
        foreach (var company in companies)
        {
            var companyEntries = await _companiesModuleApi.GetCompanyAuditAsync(
                company.CompanyId,
                normalizedFromUtc,
                normalizedToUtc,
                entityName,
                actionFilter);
            allEntries.AddRange(companyEntries);
        }

        IEnumerable<CompanyAuditEntryContract> filtered = allEntries;
        if (!string.IsNullOrWhiteSpace(actor))
        {
            filtered = filtered.Where(e => e.UserName.Contains(actor, StringComparison.OrdinalIgnoreCase));
        }

        if (entityGuid.HasValue)
        {
            filtered = filtered.Where(e => e.EntityId == entityGuid.Value);
        }

        var ordered = filtered
            .OrderByDescending(e => e.AtUtc)
            .ToList();

        var totalCount = ordered.Count;
        var pagedItems = ordered
            .Skip((normalizedPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var filterModel = new AdminAuditLogFilterViewModel
        {
            FromUtc = normalizedFromUtc,
            ToUtc = normalizedToUtc,
            EntityName = entityName,
            Action = actionFilter,
            Actor = actor,
            EntityId = entityGuid?.ToString(),
            Page = normalizedPage,
            PageSize = pageSize
        };

        var model = new AdminAuditLogListViewModel
        {
            Filter = filterModel,
            Items = pagedItems.Select(item => new AdminAuditLogListItemViewModel
            {
                Id = item.Id,
                CompanyId = item.CompanyId,
                UserName = item.UserName,
                EntityName = item.EntityName,
                EntityId = item.EntityId,
                Action = item.Action,
                AtUtc = item.AtUtc,
                ChangesJson = item.ChangesJson
            }).ToList(),
            TotalCount = totalCount,
            Page = normalizedPage,
            PageSize = pageSize
        };

        return View(model);
    }

    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Local).ToUniversalTime()
        };
    }
}
