using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels;
using WebAppClient.Services;

namespace WebApp.Areas.Company.Controllers;

public class AuditController : CompanyBaseController
{
    public AuditController(IApiClient apiClient) : base(apiClient)
    {
    }

    [HttpGet]
    public async Task<IActionResult> CompanyLog(DateTime? fromUtc = null, DateTime? toUtc = null, string? entityName = null, string? actionFilter = null)
    {
        var company = await ResolveCompanyAsync();
        if (company == null || !HasManagerAccess(company.Value.Role))
        {
            return Forbid();
        }

        return View(new CompanyAuditViewModel
        {
            CompanyId = company.Value.CompanyId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            EntityName = entityName,
            Action = actionFilter,
            Entries = new List<AuditEntryViewModel>()
        });
    }
}
