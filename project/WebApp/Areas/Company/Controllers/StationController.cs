using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using WebApp.Areas.Company.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize]
public class StationController : Controller
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private static string R(string key) => App.Resources.Views.Shared._Layout.ResourceManager.GetString(key) ?? key;

    public StationController(
        IChargingModuleApi chargingModuleApi,
        ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        var stations = await _chargingModuleApi.GetCompanyStationsAsync(resolvedCompany.Value);
        var maintenance = await _chargingModuleApi.GetMaintenancesByCompanyAsync(resolvedCompany.Value, includeResolved: true);
        var issueCounts = maintenance
            .GroupBy(m => m.ChargingStationId)
            .ToDictionary(g => g.Key, g => g.Count());

        var model = new CompanyStationListViewModel
        {
            CompanyId = resolvedCompany.Value,
            Stations = stations.Select(station => new CompanyStationItemViewModel
            {
                Id = station.Id,
                Name = station.Name,
                Location = station.Location,
                Status = station.Status,
                PricePerKwh = station.PricePerKwh,
                MaxPower = station.MaxPower,
                IsActive = station.IsActive,
                Connectors = station.Connectors.Select(c => c.Name).ToList(),
                MaintenanceIssueCount = issueCounts.TryGetValue(station.Id, out var count) ? count : 0
            }).ToList()
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

        var connectors = await _chargingModuleApi.GetConnectorsAsync(includeInactive: true);
        return View(new CompanyStationFormViewModel
        {
            CompanyId = resolvedCompany.Value,
            Status = EStationStatus.Available,
            IsActive = true,
            AvailableConnectors = connectors.Select(connector => new StationConnectorViewModel
            {
                ConnectorId = connector.Id,
                ConnectorName = connector.Name,
                IsAssigned = false
            }).ToList()
        });
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

        try
        {
            var created = await _chargingModuleApi.CreateCompanyStationAsync(new UpsertCompanyStationContract
            {
                CompanyId = resolvedCompany.Value,
                NameEn = model.NameEn,
                NameEt = model.NameEt,
                Location = model.Location,
                Status = model.Status,
                PricePerKwh = model.PricePerKwh,
                MaxPower = model.MaxPower,
                IsActive = model.IsActive
            });
            await _chargingModuleApi.SetStationConnectorsAsync(created.Id, CleanConnectorIds(model.SelectedConnectorIds));
        }
        catch (Exception)
        {
            ModelState.AddModelError(string.Empty, R("UnableToCreateStation"));
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

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(id, resolvedCompany.Value);
        if (station == null)
        {
            return Forbid();
        }

        var assignedConnectorIds = await _chargingModuleApi.GetStationAssignedConnectorIdsAsync(id);
        var connectors = await _chargingModuleApi.GetConnectorsAsync(includeInactive: true);
        return View(new CompanyStationFormViewModel
        {
            Id = station.Id,
            CompanyId = resolvedCompany.Value,
            NameEn = station.NameTranslations.TryGetValue("en", out var enName) ? enName : station.Name,
            NameEt = station.NameTranslations.TryGetValue("et", out var etName) ? etName : station.Name,
            Location = station.Location,
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            Status = station.Status,
            IsActive = station.IsActive,
            SelectedConnectorIds = assignedConnectorIds.ToList(),
            AvailableConnectors = connectors.Select(connector => new StationConnectorViewModel
            {
                ConnectorId = connector.Id,
                ConnectorName = connector.Name,
                IsAssigned = assignedConnectorIds.Contains(connector.Id)
            }).ToList()
        });
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

        var updated = await _chargingModuleApi.UpdateCompanyStationAsync(new UpsertCompanyStationContract
        {
            StationId = id,
            CompanyId = resolvedCompany.Value,
            NameEn = model.NameEn,
            NameEt = model.NameEt,
            Location = model.Location,
            Status = model.Status,
            PricePerKwh = model.PricePerKwh,
            MaxPower = model.MaxPower,
            IsActive = model.IsActive
        });
        if (updated == null)
        {
            return Forbid();
        }
        await _chargingModuleApi.SetStationConnectorsAsync(id, CleanConnectorIds(model.SelectedConnectorIds));

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

        var deleted = await _chargingModuleApi.DeleteCompanyStationAsync(id, resolvedCompany.Value);
        if (!deleted)
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

        var updated = await _chargingModuleApi.UpdateStationStatusAsync(id, status);
        if (!updated)
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

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(id, resolvedCompany.Value);
        if (station == null)
        {
            return Forbid();
        }

        var maintenanceResult = await _chargingModuleApi.GetMaintenancesByCompanyAsync(resolvedCompany.Value, includeResolved: true);

        var recentMaintenance = maintenanceResult
            .Where(issue => issue.ChargingStationId == id)
            .OrderByDescending(issue => issue.ReportedAtUtc)
            .Take(5)
            .Select(issue => new MaintenanceQueueItemViewModel
            {
                Id = issue.Id,
                StationId = issue.ChargingStationId,
                StationName = issue.StationName,
                IssueDescription = issue.IssueDescription,
                Status = issue.Status,
                ReportedAtUtc = issue.ReportedAtUtc,
                ResolvedAtUtc = issue.ResolvedAtUtc,
                AssignedToUserId = issue.AssignedToUserId,
                AssignedToUserName = issue.AssignedToUserId?.ToString() ?? string.Empty,
                ReporterUserName = issue.ReportedByUserId?.ToString() ?? string.Empty,
                Notes = issue.Notes
            })
            .ToList();

        var model = new CompanyStationDetailsViewModel
        {
            CompanyId = resolvedCompany.Value,
            Id = station.Id,
            Name = station.Name,
            Location = station.Location,
            Status = station.Status,
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            IsActive = station.IsActive,
            Connectors = station.Connectors.Select(c => c.Name).ToList(),
            MaintenanceIssueCount = maintenanceResult.Count(issue => issue.ChargingStationId == id),
            RecentMaintenance = recentMaintenance
        };

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

        var availableConnectors = await _chargingModuleApi.GetConnectorsAsync(includeInactive: true);
        model.AvailableConnectors = availableConnectors
            .Select(connector => new StationConnectorViewModel
            {
                ConnectorId = connector.Id,
                ConnectorName = connector.Name,
                IsAssigned = selectedIds.Contains(connector.Id)
            })
            .ToList();
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

        var memberships = await _companiesModuleApi.GetUserCompaniesAsync(userId);
        return memberships
            .Where(m => HasManagerOrOwnerAccess(m.Role))
            .Select(m => m.CompanyId)
            .ToList();
    }

    private Guid? ResolveCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }

    private static List<Guid> CleanConnectorIds(IEnumerable<Guid> connectorIds)
    {
        return connectorIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static bool HasManagerOrOwnerAccess(string? role)
    {
        return role != null && (role.Equals("Owner", StringComparison.OrdinalIgnoreCase) || role.Equals("Manager", StringComparison.OrdinalIgnoreCase));
    }
}
