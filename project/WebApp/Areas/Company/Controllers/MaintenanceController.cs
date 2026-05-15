using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using Shared.Contracts.Tenancy;
using Shared.Contracts.Users;
using WebApp.Areas.Company.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize]
public class MaintenanceController : Controller
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private readonly IUsersModuleApi _usersModuleApi;
    private readonly ITenantContext _tenantContext;

    public MaintenanceController(
        IChargingModuleApi chargingModuleApi,
        ICompaniesModuleApi companiesModuleApi,
        IUsersModuleApi usersModuleApi,
        ITenantContext tenantContext)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
        _usersModuleApi = usersModuleApi;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null, bool includeResolved = true)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var issues = await _chargingModuleApi.GetMaintenancesByCompanyAsync(resolvedCompany.Value, includeResolved);
        var mappedIssues = await MapIssuesAsync(issues);

        var model = new MaintenanceListViewModel
        {
            CompanyId = resolvedCompany.Value,
            IncludeResolved = includeResolved,
            CanAccessDashboard = await CanAccessDashboardAsync(resolvedCompany.Value),
            Issues = mappedIssues
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

        var issue = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, resolvedCompany.Value);
        if (issue == null)
        {
            return Forbid();
        }

        var historyResult = await _companiesModuleApi.GetAuditTrailAsync("Maintenance", id, resolvedCompany.Value);
        var mappedIssue = await MapIssueAsync(issue);
        var model = new MaintenanceDetailsViewModel
        {
            CompanyId = resolvedCompany.Value,
            Id = mappedIssue.Id,
            StationId = mappedIssue.StationId,
            StationName = mappedIssue.StationName,
            IssueDescription = mappedIssue.IssueDescription,
            Status = mappedIssue.Status,
            ReportedAtUtc = mappedIssue.ReportedAtUtc,
            ResolvedAtUtc = mappedIssue.ResolvedAtUtc,
            AssignedToUserId = mappedIssue.AssignedToUserId,
            AssignedToUserName = mappedIssue.AssignedToUserName,
            ReporterUserName = mappedIssue.ReporterUserName,
            Notes = mappedIssue.Notes,
            StatusHistory = historyResult.Entries.Select(item => new MaintenanceStatusHistoryViewModel
            {
                AtUtc = item.AtUtc,
                Action = item.Action,
                Actor = item.UserName,
                Changes = item.ChangesJson ?? string.Empty
            }).ToList()
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

        var issue = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, resolvedCompany.Value);
        if (issue == null)
        {
            return Forbid();
        }

        var actorUserName = await _usersModuleApi.GetUserDisplayNameAsync(userId.Value) ?? userId.Value.ToString();
        var updated = await _chargingModuleApi.UpdateMaintenanceStatusAsync(
            id,
            status,
            notes,
            status == EMaintenanceStatus.Resolved ? DateTime.UtcNow : null,
            actorUserName: actorUserName);
        if (!updated)
        {
            return Forbid();
        }

        if (status == EMaintenanceStatus.InProgress)
        {
            await _chargingModuleApi.UpdateStationStatusAsync(issue.ChargingStationId, EStationStatus.Maintenance);
        }
        else if (status == EMaintenanceStatus.Resolved)
        {
            await _chargingModuleApi.UpdateStationStatusAsync(issue.ChargingStationId, EStationStatus.Available);
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

        var issue = await _chargingModuleApi.GetMaintenanceByIdForCompanyAsync(id, resolvedCompany.Value);
        if (issue == null)
        {
            return Forbid();
        }

        var actorUserName = await _usersModuleApi.GetUserDisplayNameAsync(userId.Value) ?? userId.Value.ToString();
        var assigned = await _chargingModuleApi.AssignMaintenanceAsync(id, assignedToUserId, actorUserName: actorUserName);
        if (!assigned)
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Details), new { id, companyId = resolvedCompany.Value });
    }

    private async Task<Guid?> ResolveCompanyAsync(Guid? requestedCompanyId)
    {
        var membershipCompanyIds = await ResolveMembershipCompanyIdsAsync();
        if (membershipCompanyIds.Count == 0)
        {
            return null;
        }

        var resolvedCompanyId = requestedCompanyId ?? _tenantContext.CompanyId ?? membershipCompanyIds[0];
        return membershipCompanyIds.Contains(resolvedCompanyId) ? resolvedCompanyId : null;
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
            .Select(m => m.CompanyId)
            .ToList();
    }

    private Guid? ResolveCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }

    private async Task<bool> CanAccessDashboardAsync(Guid companyId)
    {
        var userId = ResolveCurrentUserId();
        if (userId == null)
        {
            return false;
        }

        var membership = await _companiesModuleApi.GetActiveCompanySelectionAsync(userId.Value, companyId);
        if (membership == null)
        {
            return false;
        }

        return IsAtLeastManagerRole(membership.Role);
    }

    private async Task<List<MaintenanceQueueItemViewModel>> MapIssuesAsync(IReadOnlyCollection<MaintenanceContract> issues)
    {
        var mapped = new List<MaintenanceQueueItemViewModel>(issues.Count);
        foreach (var issue in issues)
        {
            mapped.Add(await MapIssueAsync(issue));
        }

        return mapped;
    }

    private async Task<MaintenanceQueueItemViewModel> MapIssueAsync(MaintenanceContract issue)
    {
        var reporterUserName = await ResolveUserDisplayNameAsync(issue.ReportedByUserId);
        var assignedUserName = await ResolveUserDisplayNameAsync(issue.AssignedToUserId);
        return new MaintenanceQueueItemViewModel
        {
            Id = issue.Id,
            StationId = issue.ChargingStationId,
            StationName = issue.StationName,
            IssueDescription = issue.IssueDescription,
            Status = issue.Status,
            ReportedAtUtc = issue.ReportedAtUtc,
            ResolvedAtUtc = issue.ResolvedAtUtc,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = assignedUserName ?? string.Empty,
            ReporterUserName = reporterUserName ?? string.Empty,
            Notes = issue.Notes
        };
    }

    private async Task<string?> ResolveUserDisplayNameAsync(Guid? userId)
    {
        if (userId == null || userId == Guid.Empty)
        {
            return null;
        }

        return await _usersModuleApi.GetUserDisplayNameAsync(userId.Value);
    }

    private static bool IsAtLeastManagerRole(string? role)
    {
        return string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);
    }
}
