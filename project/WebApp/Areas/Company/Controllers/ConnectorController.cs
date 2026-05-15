using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize]
public class ConnectorController : Controller
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public ConnectorController(IChargingModuleApi chargingModuleApi, ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
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

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, resolvedCompany.Value);
        if (station == null)
        {
            return Forbid();
        }

        var assignedConnectorIds = (await _chargingModuleApi.GetStationAssignedConnectorIdsAsync(stationId)).ToHashSet();
        assignedConnectorIds.Add(connectorId);
        await _chargingModuleApi.SetStationConnectorsAsync(stationId, assignedConnectorIds.ToList());

        if (await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, resolvedCompany.Value) == null)
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

        var station = await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, resolvedCompany.Value);
        if (station == null)
        {
            return Forbid();
        }

        var assignedConnectorIds = (await _chargingModuleApi.GetStationAssignedConnectorIdsAsync(stationId)).ToHashSet();
        assignedConnectorIds.Remove(connectorId);
        await _chargingModuleApi.SetStationConnectorsAsync(stationId, assignedConnectorIds.ToList());

        if (await _chargingModuleApi.GetCompanyStationByIdAsync(stationId, resolvedCompany.Value) == null)
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

        var memberships = await _companiesModuleApi.GetUserCompaniesAsync(userId);
        return memberships
            .Where(m => HasManagerAccess(m.Role))
            .Select(m => m.CompanyId)
            .ToList();
    }

    private static bool HasManagerAccess(string role)
    {
        return role.Equals("Manager", StringComparison.OrdinalIgnoreCase)
               || role.Equals("Owner", StringComparison.OrdinalIgnoreCase);
    }
}
