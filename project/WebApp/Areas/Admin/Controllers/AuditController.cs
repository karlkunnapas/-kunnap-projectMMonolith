using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root")]
public class AuditController : Controller
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Trail(string entityName, Guid entityId, Guid? companyId = null)
    {
        if (string.IsNullOrWhiteSpace(entityName) || entityId == Guid.Empty)
        {
            return BadRequest();
        }

        var result = await _auditService.GetAuditTrailAsync(entityName, entityId, companyId);
        if (!result.Success || result.Data == null)
        {
            return NotFound();
        }

        var model = new AuditTrailViewModel
        {
            EntityName = result.Data.EntityName,
            EntityId = result.Data.EntityId,
            Entries = result.Data.Entries
                .Select(e => new AuditEntryViewModel
                {
                    Id = e.Id,
                    Action = e.Action,
                    UserName = e.UserName,
                    EntityName = e.EntityName,
                    EntityId = e.EntityId,
                    AtUtc = e.AtUtc,
                    ChangesSummary = e.ChangesJson ?? string.Empty
                })
                .ToList()
        };

        return View(model);
    }
}

