using System.Security.Claims;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApp.Areas.Company.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize(Roles = "CompanyOwner")]
public class MaintenanceController : Controller
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly IChargingStationCompanyService _chargingStationCompanyService;
    private readonly AppDbContext _context;

    public MaintenanceController(
        IMaintenanceService maintenanceService,
        IChargingStationCompanyService chargingStationCompanyService,
        AppDbContext context)
    {
        _maintenanceService = maintenanceService;
        _chargingStationCompanyService = chargingStationCompanyService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null, bool includeResolved = true)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var result = await _maintenanceService.GetIssuesAsync(resolvedCompany.Value, includeResolved);
        if (!result.Success)
        {
            return Forbid();
        }

        var model = new MaintenanceListViewModel
        {
            CompanyId = resolvedCompany.Value,
            IncludeResolved = includeResolved,
            Issues = result.Data?.Select(MapIssue).ToList() ?? new List<MaintenanceQueueItemViewModel>()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? companyId = null, Guid? stationId = null)
    {
        return Forbid();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid? companyId, MaintenanceCreateViewModel model)
    {
        return Forbid();
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var issueResult = await _maintenanceService.GetByIdAsync(id, resolvedCompany.Value);
        if (!issueResult.Success || issueResult.Data == null)
        {
            return Forbid();
        }

        var historyResult = await _maintenanceService.GetStatusHistoryAsync(id, resolvedCompany.Value);
        var model = new MaintenanceDetailsViewModel
        {
            CompanyId = resolvedCompany.Value,
            Id = issueResult.Data.Id,
            StationId = issueResult.Data.StationId,
            StationName = issueResult.Data.StationName,
            IssueDescription = issueResult.Data.IssueDescription,
            Status = issueResult.Data.Status,
            ReportedAtUtc = issueResult.Data.ReportedAtUtc,
            ResolvedAtUtc = issueResult.Data.ResolvedAtUtc,
            AssignedToUserId = issueResult.Data.AssignedToUserId,
            AssignedToUserName = issueResult.Data.AssignedToUserName,
            ReporterUserName = issueResult.Data.ReporterUserName,
            Notes = issueResult.Data.Notes,
            StatusHistory = historyResult.Data?.Select(item => new MaintenanceStatusHistoryViewModel
            {
                AtUtc = item.AtUtc,
                Action = item.Action,
                Actor = item.Actor,
                Changes = item.Changes
            }).ToList() ?? new List<MaintenanceStatusHistoryViewModel>()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid companyId, Guid id, EMaintenanceStatus status, string? notes)
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

        var result = await _maintenanceService.UpdateStatusAsync(id, resolvedCompany.Value, userId.Value, status, notes);
        if (!result.Success)
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Details), new { id, companyId = resolvedCompany.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(Guid companyId, Guid id, Guid? assignedToUserId)
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

        var result = await _maintenanceService.AssignAsync(id, resolvedCompany.Value, userId.Value, assignedToUserId);
        if (!result.Success)
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Details), new { id, companyId = resolvedCompany.Value });
    }

    private async Task PopulateStationSelectListAsync(Guid companyId, Guid? selectedStationId)
    {
        var stationsResult = await _chargingStationCompanyService.GetCompanyStationsAsync(companyId);
        var items = stationsResult.Data?
                        .Select(station => new SelectListItem
                        {
                            Value = station.Id.ToString(),
                            Text = $"{station.Name} ({station.Location})"
                        })
                        .ToList() ?? new List<SelectListItem>();

        ViewBag.StationOptions = new SelectList(items, "Value", "Text", selectedStationId?.ToString());
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
            .Where(uc => uc.AppUserId == userId && uc.IsActive)
            .OrderByDescending(uc => uc.JoinedAtUtc)
            .Select(uc => uc.CompanyId)
            .ToListAsync();
    }

    private Guid? ResolveCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }

    private static MaintenanceQueueItemViewModel MapIssue(App.BLL.DTOs.MaintenanceIssueDto issue)
    {
        return new MaintenanceQueueItemViewModel
        {
            Id = issue.Id,
            StationId = issue.StationId,
            StationName = issue.StationName,
            IssueDescription = issue.IssueDescription,
            Status = issue.Status,
            ReportedAtUtc = issue.ReportedAtUtc,
            ResolvedAtUtc = issue.ResolvedAtUtc,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = issue.AssignedToUserName,
            ReporterUserName = issue.ReporterUserName,
            Notes = issue.Notes
        };
    }
}
