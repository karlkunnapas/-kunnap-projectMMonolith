using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Company.ViewModels;
using WebAppClient.Helpers;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Areas.Company.Controllers;

public class StationController : CompanyBaseController
{
    public StationController(IApiClient apiClient) : base(apiClient)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var stations = await ApiClient.GetAsync<List<CompanyStationResponseDto>>($"api/v1/company/{company.Value.CompanyId}/station");
        var localizedNameByStationId = await GetLocalizedStationNameMapAsync();
        return View(new CompanyStationListViewModel
        {
            CompanyId = company.Value.CompanyId,
            Stations = stations.Select(s => MapStation(s, localizedNameByStationId)).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var form = await ApiClient.GetAsync<CompanyStationFormResponseDto>($"api/v1/company/{company.Value.CompanyId}/station/form");
        return View(MapForm(form));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompanyStationFormViewModel model)
    {
        var company = await ResolveCompanyAsync(model.CompanyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await PopulateConnectorOptionsAsync(model, company.Value.CompanyId, model.Id);
            return View(model);
        }

        try
        {
            await ApiClient.PostAsync<CompanyStationResponseDto>(
                $"api/v1/company/{company.Value.CompanyId}/station",
                MapUpsert(model));
            return RedirectToAction(nameof(Index), new { companyId = company.Value.CompanyId });
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateConnectorOptionsAsync(model, company.Value.CompanyId, model.Id);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var form = await ApiClient.GetAsync<CompanyStationFormResponseDto>($"api/v1/company/{company.Value.CompanyId}/station/form?stationId={id}");
        return View(MapForm(form));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CompanyStationFormViewModel model)
    {
        var company = await ResolveCompanyAsync(model.CompanyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await PopulateConnectorOptionsAsync(model, company.Value.CompanyId, id);
            return View(model);
        }

        try
        {
            await ApiClient.PutAsync<CompanyStationResponseDto>(
                $"api/v1/company/{company.Value.CompanyId}/station/{id}",
                MapUpsert(model));
            return RedirectToAction(nameof(Details), new { id, companyId = company.Value.CompanyId });
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateConnectorOptionsAsync(model, company.Value.CompanyId, id);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid companyId, Guid id)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        await ApiClient.DeleteAsync($"api/v1/company/{company.Value.CompanyId}/station/{id}");
        return RedirectToAction(nameof(Index), new { companyId = company.Value.CompanyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(Guid companyId, Guid id, WebAppClient.Enums.EStationStatus status)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        await ApiClient.PatchAsync<CompanyStationResponseDto>(
            $"api/v1/company/{company.Value.CompanyId}/station/{id}/status",
            new StationStatusUpdateRequestDto { Status = (int)status });
        return RedirectToAction(nameof(Details), new { id, companyId = company.Value.CompanyId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var station = await ApiClient.GetAsync<CompanyStationResponseDto>($"api/v1/company/{company.Value.CompanyId}/station/{id}");
        var maintenance = await ApiClient.GetAsync<List<MaintenanceIssueResponseDto>>($"api/v1/company/{company.Value.CompanyId}/maintenance?includeResolved=true");
        var localizedNameByStationId = await GetLocalizedStationNameMapAsync();
        var recentMaintenance = maintenance
            .Where(m => m.StationId == id)
            .OrderByDescending(m => m.ReportedAtUtc)
            .Take(5)
            .Select(m => new MaintenanceQueueItemViewModel
            {
                Id = m.Id,
                StationId = m.StationId,
                StationName = localizedNameByStationId.GetValueOrDefault(m.StationId, m.StationName),
                IssueDescription = m.IssueDescription,
                Status = EnumParser.ParseMaintenance(m.Status),
                ReportedAtUtc = m.ReportedAtUtc,
                ResolvedAtUtc = m.ResolvedAtUtc,
                AssignedToUserId = m.AssignedToUserId,
                AssignedToUserName = m.AssignedToUserName,
                ReporterUserName = m.ReporterUserName,
                Notes = m.Notes
            }).ToList();

        return View(new CompanyStationDetailsViewModel
        {
            CompanyId = company.Value.CompanyId,
            Id = station.Id,
            Name = localizedNameByStationId.GetValueOrDefault(station.Id, station.Name),
            Location = station.Location,
            Status = EnumParser.ParseStation(station.Status),
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            IsActive = station.IsActive,
            Connectors = station.Connectors,
            MaintenanceIssueCount = station.MaintenanceIssueCount,
            RecentMaintenance = recentMaintenance
        });
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> GetById(Guid id, Guid? companyId = null)
    {
        return Details(id, companyId);
    }

    private static CompanyStationItemViewModel MapStation(CompanyStationResponseDto dto, IReadOnlyDictionary<Guid, string> localizedNameByStationId)
    {
        return new CompanyStationItemViewModel
        {
            Id = dto.Id,
            Name = localizedNameByStationId.GetValueOrDefault(dto.Id, dto.Name),
            Location = dto.Location,
            Status = EnumParser.ParseStation(dto.Status),
            PricePerKwh = dto.PricePerKwh,
            MaxPower = dto.MaxPower,
            IsActive = dto.IsActive,
            Connectors = dto.Connectors,
            MaintenanceIssueCount = dto.MaintenanceIssueCount
        };
    }

    private static CompanyStationFormViewModel MapForm(CompanyStationFormResponseDto dto)
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
            Status = EnumParser.ParseStation(dto.Status),
            IsActive = dto.IsActive,
            SelectedConnectorIds = dto.SelectedConnectorIds,
            AvailableConnectors = dto.AvailableConnectors.Select(c => new StationConnectorViewModel
            {
                ConnectorId = c.ConnectorId,
                ConnectorName = c.ConnectorName,
                IsAssigned = c.IsAssigned
            }).ToList()
        };
    }

    private static CompanyStationUpsertRequestDto MapUpsert(CompanyStationFormViewModel model)
    {
        return new CompanyStationUpsertRequestDto
        {
            NameEn = model.NameEn,
            NameEt = model.NameEt,
            Location = model.Location,
            PricePerKwh = model.PricePerKwh,
            MaxPower = model.MaxPower,
            Status = (int)model.Status,
            IsActive = model.IsActive,
            SelectedConnectorIds = model.SelectedConnectorIds.Where(x => x != Guid.Empty).Distinct().ToList()
        };
    }

    private async Task PopulateConnectorOptionsAsync(CompanyStationFormViewModel model, Guid companyId, Guid? stationId)
    {
        var form = await ApiClient.GetAsync<CompanyStationFormResponseDto>(
            stationId.HasValue
                ? $"api/v1/company/{companyId}/station/form?stationId={stationId.Value}"
                : $"api/v1/company/{companyId}/station/form");
        model.AvailableConnectors = form.AvailableConnectors.Select(c => new StationConnectorViewModel
        {
            ConnectorId = c.ConnectorId,
            ConnectorName = c.ConnectorName,
            IsAssigned = model.SelectedConnectorIds.Contains(c.ConnectorId)
        }).ToList();
    }
}
