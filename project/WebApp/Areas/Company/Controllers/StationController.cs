using System.Security.Claims;
using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Areas.Company.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize]
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
    public async Task<IActionResult> Create(Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var result = await _stationService.GetCreateFormAsync(resolvedCompany.Value);
        if (!result.Success || result.Data == null)
        {
            return Forbid();
        }

        return View(MapForm(result.Data));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompanyStationFormViewModel model)
    {
        var resolvedCompany = await ResolveCompanyAsync(model.CompanyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await PopulateConnectorOptionsAsync(model, resolvedCompany.Value);
            return View(model);
        }

        var userId = ResolveCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _stationService.CreateStationAsync(
            resolvedCompany.Value,
            userId.Value,
            User.Identity?.Name ?? userId.Value.ToString(),
            MapUpsert(model));

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }

            await PopulateConnectorOptionsAsync(model, resolvedCompany.Value);
            return View(model);
        }

        return RedirectToAction(nameof(Index), new { companyId = resolvedCompany.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var result = await _stationService.GetEditFormAsync(id, resolvedCompany.Value);
        if (!result.Success || result.Data == null)
        {
            return Forbid();
        }

        return View(MapForm(result.Data));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CompanyStationFormViewModel model)
    {
        var resolvedCompany = await ResolveCompanyAsync(model.CompanyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        if (id == Guid.Empty || model.Id == null || id != model.Id.Value)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await PopulateConnectorOptionsAsync(model, resolvedCompany.Value);
            return View(model);
        }

        var userId = ResolveCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _stationService.UpdateStationAsync(
            id,
            resolvedCompany.Value,
            userId.Value,
            User.Identity?.Name ?? userId.Value.ToString(),
            MapUpsert(model));

        if (!result.Success)
        {
            if (result.Errors.Any(error => error.Code == "FORBIDDEN"))
            {
                return Forbid();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }

            await PopulateConnectorOptionsAsync(model, resolvedCompany.Value);
            return View(model);
        }

        return RedirectToAction(nameof(Details), new { id, companyId = resolvedCompany.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid companyId, Guid id)
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

        var result = await _stationService.DeleteStationAsync(
            id,
            resolvedCompany.Value,
            userId.Value,
            User.Identity?.Name ?? userId.Value.ToString());

        if (!result.Success && result.Errors.Any(error => error.Code == "FORBIDDEN"))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new { companyId = resolvedCompany.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(Guid companyId, Guid id, EStationStatus status)
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

        var result = await _stationService.UpdateStatusAsync(
            id,
            resolvedCompany.Value,
            userId.Value,
            User.Identity?.Name ?? userId.Value.ToString(),
            status);

        if (!result.Success && result.Errors.Any(error => error.Code == "FORBIDDEN"))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Details), new { id, companyId = resolvedCompany.Value });
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

    [HttpGet("{id:guid}")]
    public Task<IActionResult> GetById(Guid id, Guid? companyId = null)
    {
        return Details(id, companyId);
    }

    private async Task PopulateConnectorOptionsAsync(CompanyStationFormViewModel model, Guid companyId)
    {
        var selectedIds = model.SelectedConnectorIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var formResult = model.Id.HasValue
            ? await _stationService.GetEditFormAsync(model.Id.Value, companyId)
            : await _stationService.GetCreateFormAsync(companyId);

        model.AvailableConnectors = formResult.Data?.AvailableConnectors
            .Select(connector => new StationConnectorViewModel
            {
                ConnectorId = connector.ConnectorId,
                ConnectorName = connector.ConnectorName,
                IsAssigned = selectedIds.Contains(connector.ConnectorId)
            })
            .ToList() ?? new List<StationConnectorViewModel>();
    }

    private static CompanyStationFormViewModel MapForm(CompanyStationFormDto dto)
    {
        return new CompanyStationFormViewModel
        {
            Id = dto.Id,
            CompanyId = dto.CompanyId,
            NameEn = dto.NameEn,
            NameEt = dto.NameEt,
            Location = dto.Location,
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            Status = dto.Status,
            IsActive = dto.IsActive,
            SelectedConnectorIds = dto.SelectedConnectorIds,
            AvailableConnectors = dto.AvailableConnectors.Select(connector => new StationConnectorViewModel
            {
                ConnectorId = connector.ConnectorId,
                ConnectorName = connector.ConnectorName,
                IsAssigned = connector.IsAssigned
            }).ToList()
        };
    }

    private static CompanyStationUpsertDto MapUpsert(CompanyStationFormViewModel model)
    {
        return new CompanyStationUpsertDto
        {
            NameEn = model.NameEn,
            NameEt = model.NameEt,
            Location = model.Location,
            PricePerKwh = model.PricePerKwh,
            MaxPower = model.MaxPower,
            Status = model.Status,
            IsActive = model.IsActive,
            SelectedConnectorIds = model.SelectedConnectorIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList()
        };
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
            .Where(uc => uc.AppUserId == userId && uc.IsActive && uc.Role >= ECompanyRole.Manager)
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
