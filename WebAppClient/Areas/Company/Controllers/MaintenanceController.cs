using Microsoft.AspNetCore.Mvc;
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
        if (company == null || !HasManagerAccess(company.Value.Role))
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
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        await ApiClient.PatchAsync<MaintenanceIssueResponseDto>(
            $"api/v1/company/{company.Value.CompanyId}/maintenance/{id}/assign",
            new MaintenanceAssignmentRequestDto { AssignedToUserId = assignedToUserId });

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
