using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebApp.Areas.Company.ViewModels;
using WebAppClient.Helpers;
using WebAppClient.Models;
using WebAppClient.Services;
using WebAppClient.Enums;

namespace WebApp.Areas.Company.Controllers;

public class MaintenanceController : CompanyBaseController
{
    public MaintenanceController(IApiClient apiClient) : base(apiClient)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null, bool includeResolved = true)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null)
        {
            return Forbid();
        }

        var issues = await ApiClient.GetAsync<List<MaintenanceIssueResponseDto>>(
            $"api/v1/company/{company.Value.CompanyId}/maintenance?includeResolved={includeResolved.ToString().ToLowerInvariant()}");
        var localizedNameByStationId = await GetLocalizedStationNameMapAsync();

        return View(new MaintenanceListViewModel
        {
            CompanyId = company.Value.CompanyId,
            IncludeResolved = includeResolved,
            CanAccessDashboard = HasManagerAccess(company.Value.Role),
            Issues = issues.Select(i => MapIssue(i, localizedNameByStationId)).ToList()
        });
    }

    [HttpGet]
    public IActionResult Create(Guid? companyId = null, Guid? stationId = null)
    {
        return Forbid();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Guid? companyId, MaintenanceCreateViewModel model)
    {
        return Forbid();
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null)
        {
            return Forbid();
        }

        var issue = await ApiClient.GetAsync<MaintenanceIssueResponseDto>($"api/v1/company/{company.Value.CompanyId}/maintenance/{id}");
        var history = await ApiClient.GetAsync<List<MaintenanceStatusHistoryResponseDto>>($"api/v1/company/{company.Value.CompanyId}/maintenance/{id}/history");
        var localizedNameByStationId = await GetLocalizedStationNameMapAsync();

        return View(new MaintenanceDetailsViewModel
        {
            CompanyId = company.Value.CompanyId,
            Id = issue.Id,
            StationId = issue.StationId,
            StationName = localizedNameByStationId.GetValueOrDefault(issue.StationId, issue.StationName),
            IssueDescription = issue.IssueDescription,
            Status = EnumParser.ParseMaintenance(issue.Status),
            ReportedAtUtc = issue.ReportedAtUtc,
            ResolvedAtUtc = issue.ResolvedAtUtc,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = issue.AssignedToUserName,
            ReporterUserName = issue.ReporterUserName,
            Notes = issue.Notes,
            StatusHistory = history.Select(h => new MaintenanceStatusHistoryViewModel
            {
                AtUtc = h.AtUtc,
                Action = h.Action,
                Actor = h.Actor,
                Changes = h.Changes
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid companyId, Guid id, EMaintenanceStatus status, string? notes)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasEmployeeAccess(company.Value.Role))
        {
            return Forbid();
        }

        await ApiClient.PatchAsync<MaintenanceIssueResponseDto>(
            $"api/v1/company/{company.Value.CompanyId}/maintenance/{id}/status",
            new MaintenanceStatusUpdateRequestDto
            {
                Status = (int)status,
                Notes = notes
            });

        return RedirectToAction(nameof(Details), new { id, companyId = company.Value.CompanyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(Guid companyId, Guid id, Guid? assignedToUserId)
    {
        return await AssignInternal(companyId, id, assignedToUserId, successMessageKey: "AssignmentUpdatedSuccess");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignToMe(Guid companyId, Guid id)
    {
        var currentUserIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(currentUserIdValue, out var currentUserId))
        {
            TempData["MaintenanceAssignErrorKey"] = "CurrentUserResolutionFailed";
            return RedirectToAction(nameof(Details), new { id, companyId });
        }

        return await AssignInternal(companyId, id, currentUserId, successMessageKey: "AssignedToYouSuccess");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAssignment(Guid companyId, Guid id)
    {
        return await AssignInternal(companyId, id, null, successMessageKey: "AssignmentClearedSuccess");
    }

    private async Task<IActionResult> AssignInternal(Guid companyId, Guid id, Guid? assignedToUserId, string successMessageKey)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null)
        {
            return Forbid();
        }

        try
        {
            await ApiClient.PatchAsync<MaintenanceIssueResponseDto>(
                $"api/v1/company/{company.Value.CompanyId}/maintenance/{id}/assign",
                new MaintenanceAssignmentRequestDto { AssignedToUserId = assignedToUserId });

            TempData["MaintenanceAssignSuccessKey"] = successMessageKey;
        }
        catch (ApiException ex)
        {
            TempData["MaintenanceAssignError"] = ex.Message;
            TempData["MaintenanceAssignErrorKey"] = string.IsNullOrWhiteSpace(ex.Message)
                ? "AssignmentUpdateFailed"
                : null;
        }

        return RedirectToAction(nameof(Details), new { id, companyId = company.Value.CompanyId });
    }

    private static MaintenanceQueueItemViewModel MapIssue(MaintenanceIssueResponseDto issue, IReadOnlyDictionary<Guid, string> localizedNameByStationId)
    {
        return new MaintenanceQueueItemViewModel
        {
            Id = issue.Id,
            StationId = issue.StationId,
            StationName = localizedNameByStationId.GetValueOrDefault(issue.StationId, issue.StationName),
            IssueDescription = issue.IssueDescription,
            Status = EnumParser.ParseMaintenance(issue.Status),
            ReportedAtUtc = issue.ReportedAtUtc,
            ResolvedAtUtc = issue.ResolvedAtUtc,
            AssignedToUserId = issue.AssignedToUserId,
            AssignedToUserName = issue.AssignedToUserName,
            ReporterUserName = issue.ReporterUserName,
            Notes = issue.Notes
        };
    }
}
