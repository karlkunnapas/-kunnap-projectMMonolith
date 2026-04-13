using Microsoft.AspNetCore.Mvc;
using WebAppClient.Helpers;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Areas.Company.Controllers;

public class ConnectorController : CompanyBaseController
{
    public ConnectorController(IApiClient apiClient) : base(apiClient)
    {
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignConnector(Guid companyId, Guid stationId, Guid connectorId)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var form = await ApiClient.GetAsync<CompanyStationFormResponseDto>($"api/v1/company/{company.Value.CompanyId}/station/form?stationId={stationId}");
        if (!form.SelectedConnectorIds.Contains(connectorId))
        {
            form.SelectedConnectorIds.Add(connectorId);
        }

        await ApiClient.PutAsync<CompanyStationResponseDto>(
            $"api/v1/company/{company.Value.CompanyId}/station/{stationId}",
            ToUpsert(form));
        return RedirectToAction("Edit", "Station", new { id = stationId, companyId = company.Value.CompanyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveConnector(Guid companyId, Guid stationId, Guid connectorId)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var form = await ApiClient.GetAsync<CompanyStationFormResponseDto>($"api/v1/company/{company.Value.CompanyId}/station/form?stationId={stationId}");
        form.SelectedConnectorIds = form.SelectedConnectorIds.Where(id => id != connectorId).ToList();

        await ApiClient.PutAsync<CompanyStationResponseDto>(
            $"api/v1/company/{company.Value.CompanyId}/station/{stationId}",
            ToUpsert(form));
        return RedirectToAction("Edit", "Station", new { id = stationId, companyId = company.Value.CompanyId });
    }

    private static CompanyStationUpsertRequestDto ToUpsert(CompanyStationFormResponseDto form)
    {
        return new CompanyStationUpsertRequestDto
        {
            NameEn = form.NameEn,
            NameEt = form.NameEt,
            Location = form.Location,
            PricePerKwh = form.PricePerKwh,
            MaxPower = form.MaxPower,
            Status = (int)EnumParser.ParseStation(form.Status),
            IsActive = form.IsActive,
            SelectedConnectorIds = form.SelectedConnectorIds.Distinct().ToList()
        };
    }
}
