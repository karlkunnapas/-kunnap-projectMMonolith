using System.Security.Claims;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize(Roles = "CompanyOwner")]
public class ConnectorController : Controller
{
    private readonly IChargingStationCompanyService _stationService;
    private readonly AppDbContext _context;

    public ConnectorController(IChargingStationCompanyService stationService, AppDbContext context)
    {
        _stationService = stationService;
        _context = context;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignConnector(Guid companyId, Guid stationId, Guid connectorId)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var userId = ResolveCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _stationService.AssignConnectorAsync(
            stationId,
            resolvedCompany.Value,
            userId.Value,
            User.Identity?.Name ?? userId.Value.ToString(),
            connectorId);

        if (!result.Success && result.Errors.Any(error => error.Code == "FORBIDDEN"))
        {
            return Forbid();
        }

        return RedirectToAction("Edit", "Station", new { id = stationId, companyId = resolvedCompany.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveConnector(Guid companyId, Guid stationId, Guid connectorId)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var userId = ResolveCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _stationService.RemoveConnectorAsync(
            stationId,
            resolvedCompany.Value,
            userId.Value,
            User.Identity?.Name ?? userId.Value.ToString(),
            connectorId);

        if (!result.Success && result.Errors.Any(error => error.Code == "FORBIDDEN"))
        {
            return Forbid();
        }

        return RedirectToAction("Edit", "Station", new { id = stationId, companyId = resolvedCompany.Value });
    }

    private async Task<Guid?> ResolveCompanyAsync(Guid? requestedCompanyId)
    {
        var membershipCompanyIds = await ResolveMembershipCompanyIdsAsync();
        if (membershipCompanyIds.Count == 0)
        {
            return null;
        }

        var resolvedCompanyId = requestedCompanyId ?? membershipCompanyIds[0];
        return membershipCompanyIds.Contains(resolvedCompanyId) ? resolvedCompanyId : null;
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
            .Where(uc => uc.AppUserId == userId && uc.IsActive && uc.Role == ECompanyRole.Owner)
            .OrderByDescending(uc => uc.JoinedAtUtc)
            .Select(uc => uc.CompanyId)
            .ToListAsync();
    }

    private Guid? ResolveCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }
}
