using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Admin.ViewModels;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root")]
public class AuditLogsController : Controller
{
    private readonly IAdminPanelService _adminPanelService;

    public AuditLogsController(IAdminPanelService adminPanelService)
    {
        _adminPanelService = adminPanelService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? actionFilter = null,
        string? actor = null,
        string? entityId = null,
        int page = 1)
    {
        Guid? entityGuid = null;
        if (!string.IsNullOrWhiteSpace(entityId))
        {
            if (!Guid.TryParse(entityId, out var parsed))
            {
                ModelState.AddModelError(nameof(entityId), App.Resources.Views.Admin.AuditLogs.Index.InvalidEntityId);
            }
            else
            {
                entityGuid = parsed;
            }
        }

        var filterDto = BllDtoFactory.CreateAdminAuditLogFilterDto(
            NormalizeToUtc(fromUtc),
            NormalizeToUtc(toUtc),
            entityName,
            actionFilter,
            actor,
            entityGuid,
            page,
            50);

        var result = await _adminPanelService.GetAuditLogsAsync(filterDto);
        if (!result.Success || result.Data == null)
        {
            return BadRequest();
        }

        var filterModel = new AdminAuditLogFilterViewModel
        {
            FromUtc = result.Data.Filter.FromUtc,
            ToUtc = result.Data.Filter.ToUtc,
            EntityName = result.Data.Filter.EntityName,
            Action = result.Data.Filter.Action,
            Actor = result.Data.Filter.Actor,
            EntityId = result.Data.Filter.EntityId?.ToString(),
            Page = result.Data.Page,
            PageSize = result.Data.PageSize
        };

        var model = new AdminAuditLogListViewModel
        {
            Filter = filterModel,
            Items = result.Data.Items.Select(item => new AdminAuditLogListItemViewModel
            {
                Id = item.Id,
                CompanyId = item.CompanyId,
                UserName = item.UserName,
                EntityName = item.EntityName,
                EntityId = item.EntityId,
                Action = item.Action,
                AtUtc = item.AtUtc,
                ChangesJson = item.ChangesJson
            }).ToList(),
            TotalCount = result.Data.TotalCount,
            Page = result.Data.Page,
            PageSize = result.Data.PageSize
        };

        return View(model);
    }

    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Local).ToUniversalTime()
        };
    }
}
