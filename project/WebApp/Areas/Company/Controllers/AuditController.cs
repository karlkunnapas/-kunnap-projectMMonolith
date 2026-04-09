using System.Security.Claims;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize(Roles = "CompanyOwner")]
public class AuditController : Controller
{
    private readonly IAuditService _auditService;
    private readonly AppDbContext _context;

    public AuditController(IAuditService auditService, AppDbContext context)
    {
        _auditService = auditService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> CompanyLog(Guid? companyId = null, DateTime? fromUtc = null, DateTime? toUtc = null, string? entityName = null, string? action = null)
    {
        var membershipCompanyIds = await ResolveMembershipCompanyIdsAsync();
        if (membershipCompanyIds.Count == 0)
        {
            return Forbid();
        }

        var resolvedCompanyId = companyId ?? membershipCompanyIds[0];
        if (!membershipCompanyIds.Contains(resolvedCompanyId))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var normalizedFromUtc = NormalizeToUtc(fromUtc);
        var normalizedToUtc = NormalizeToUtc(toUtc);

        var result = await _auditService.GetCompanyAuditAsync(resolvedCompanyId, normalizedFromUtc, normalizedToUtc, entityName, action);
        if (!result.Success)
        {
            return Forbid();
        }

        var model = new CompanyAuditViewModel
        {
            CompanyId = resolvedCompanyId,
            FromUtc = normalizedFromUtc,
            ToUtc = normalizedToUtc,
            EntityName = entityName,
            Action = action,
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
            .Where(uc => uc.AppUserId == userId && uc.IsActive)
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
