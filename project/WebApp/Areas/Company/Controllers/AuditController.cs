using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Companies;
using Shared.Contracts.Tenancy;
using WebApp.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize]
public class AuditController : Controller
{
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private readonly ITenantContext _tenantContext;

    public AuditController(
        ICompaniesModuleApi companiesModuleApi,
        ITenantContext tenantContext)
    {
        _companiesModuleApi = companiesModuleApi;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> CompanyLog(DateTime? fromUtc = null, DateTime? toUtc = null, string? entityName = null, string? actionFilter = null)
    {
        var membershipCompanyIds = await ResolveMembershipCompanyIdsAsync();
        if (membershipCompanyIds.Count == 0)
        {
            return Forbid();
        }

        var resolvedCompanyId = _tenantContext.CompanyId;
        if (!resolvedCompanyId.HasValue)
        {
            return Forbid();
        }

        if (!membershipCompanyIds.Contains(resolvedCompanyId.Value))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var normalizedFromUtc = NormalizeToUtc(fromUtc);
        var normalizedToUtc = NormalizeToUtc(toUtc);
        var normalizedEntityName = string.IsNullOrWhiteSpace(entityName) ? null : entityName.Trim();
        var normalizedActionFilter = string.IsNullOrWhiteSpace(actionFilter) ? null : actionFilter.Trim();

        var entries = await _companiesModuleApi.GetCompanyAuditAsync(
            resolvedCompanyId.Value,
            normalizedFromUtc,
            normalizedToUtc,
            normalizedEntityName,
            normalizedActionFilter);

        var model = new CompanyAuditViewModel
        {
            CompanyId = resolvedCompanyId.Value,
            FromUtc = normalizedFromUtc,
            ToUtc = normalizedToUtc,
            EntityName = normalizedEntityName,
            Action = normalizedActionFilter,
            Entries = entries.Select(e => new AuditEntryViewModel
            {
                Id = e.Id,
                Action = e.Action,
                UserName = e.UserName,
                EntityName = e.EntityName,
                EntityId = e.EntityId,
                AtUtc = e.AtUtc,
                ChangesSummary = e.ChangesJson ?? string.Empty
            }).ToList() ?? new List<AuditEntryViewModel>()
        };

        return View(model);
    }

    private async Task<List<Guid>> ResolveMembershipCompanyIdsAsync()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return new List<Guid>();
        }

        var memberships = await _companiesModuleApi.GetUserCompaniesAsync(userId);
        return memberships
            .Where(m => HasManagerAccess(m.Role))
            .Select(m => m.CompanyId)
            .ToList();
    }

    private static bool HasManagerAccess(string role)
    {
        return role.Equals("Manager", StringComparison.OrdinalIgnoreCase)
               || role.Equals("Owner", StringComparison.OrdinalIgnoreCase);
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
