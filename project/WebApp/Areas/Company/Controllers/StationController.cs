using System.Security.Claims;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Areas.Company.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize(Roles = "CompanyOwner")]
public class StationController : Controller
{
    private readonly IChargingStationCompanyService _stationService;
    private readonly IMaintenanceService _maintenanceService;
    private readonly AppDbContext _context;

    public StationController(
        IChargingStationCompanyService stationService,
        IMaintenanceService maintenanceService,
        AppDbContext context)
    {
        _stationService = stationService;
        _maintenanceService = maintenanceService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var result = await _stationService.GetCompanyStationsAsync(resolvedCompany.Value);
        if (!result.Success)
        {
            return Forbid();
        }

        var model = new CompanyStationListViewModel
        {
            CompanyId = resolvedCompany.Value,
            Stations = result.Data?.Select(station => new CompanyStationItemViewModel
            {
                Id = station.Id,
                Name = station.Name,
                Location = station.Location,
                Status = station.Status,
                PricePerKwh = station.PricePerKwh,
                MaxPower = station.MaxPower,
                IsActive = station.IsActive,
                Connectors = station.Connectors,
                MaintenanceIssueCount = station.MaintenanceIssueCount
            }).ToList() ?? new List<CompanyStationItemViewModel>()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var stationResult = await _stationService.GetStationDetailsAsync(id, resolvedCompany.Value);
        if (!stationResult.Success || stationResult.Data == null)
        {
            return Forbid();
        }

        var maintenanceResult = await _maintenanceService.GetIssuesAsync(resolvedCompany.Value, includeResolved: true);
        if (!maintenanceResult.Success)
        {
            return Forbid();
        }

        var recentMaintenance = maintenanceResult.Data?
            .Where(issue => issue.StationId == id)
            .OrderByDescending(issue => issue.ReportedAtUtc)
            .Take(5)
            .Select(issue => new MaintenanceQueueItemViewModel
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
            })
            .ToList() ?? new List<MaintenanceQueueItemViewModel>();

        var model = new CompanyStationDetailsViewModel
        {
            Id = stationResult.Data.Id,
            Name = stationResult.Data.Name,
            Location = stationResult.Data.Location,
            Status = stationResult.Data.Status,
            PricePerKwh = stationResult.Data.PricePerKwh,
            MaxPower = stationResult.Data.MaxPower,
            IsActive = stationResult.Data.IsActive,
            Connectors = stationResult.Data.Connectors,
            MaintenanceIssueCount = stationResult.Data.MaintenanceIssueCount,
            RecentMaintenance = recentMaintenance
        };

        ViewData["CompanyId"] = resolvedCompany.Value;
        return View(model);
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
}
