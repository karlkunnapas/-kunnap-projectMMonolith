using System.Security.Claims;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize]
public class AuditController : Controller
{
    private readonly IAuditService _auditService;
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenantContext;

    public AuditController(IAuditService auditService, AppDbContext context, ITenantContext tenantContext)
    {
        _auditService = auditService;
        _context = context;
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

        var result = await _auditService.GetCompanyAuditAsync(
            resolvedCompanyId.Value,
            normalizedFromUtc,
            normalizedToUtc,
            normalizedEntityName,
            normalizedActionFilter);
        if (!result.Success)
        {
            return Forbid();
        }

        var model = new CompanyAuditViewModel
        {
            CompanyId = resolvedCompanyId.Value,
            FromUtc = normalizedFromUtc,
            ToUtc = normalizedToUtc,
            EntityName = normalizedEntityName,
            Action = normalizedActionFilter,
            Entries = result.Data?.Select(e => new AuditEntryViewModel
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

        return await _context.AppUserCompanies
            .AsNoTracking()
            .Where(uc => uc.AppUserId == userId && uc.IsActive && uc.Company != null && uc.Company.IsActive && uc.Role >= ECompanyRole.Manager)
            .OrderByDescending(uc => uc.JoinedAtUtc)
            .Select(uc => uc.CompanyId)
            .ToListAsync();
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
