using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using WebApp.Areas.Admin.ViewModels;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root,SystemAdmin")]
[Route("Admin/[controller]")]
public class ConnectorTypesController(IChargingModuleApi chargingModuleApi) : Controller
{
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? search = null)
    {
        var connectorTypes = await chargingModuleApi.GetConnectorsAsync(includeInactive: true);
        if (!string.IsNullOrWhiteSpace(search))
        {
            connectorTypes = connectorTypes
                .Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var model = new AdminConnectorTypeListViewModel
        {
            Search = search,
            Items = connectorTypes.Select(item => new AdminConnectorTypeListItemViewModel
            {
                ConnectorTypeId = item.Id,
                Name = item.Name,
                IsActive = item.IsActive
            }).ToList()
        };

        return View(model);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View("Form", new AdminConnectorTypeFormViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] AdminConnectorTypeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        try
        {
            await chargingModuleApi.CreateConnectorTypeAsync(model.NameEn, model.NameEt, model.IsActive);
        }
        catch
        {
            ModelState.AddModelError("", "Failed to create connector type.");
            return View("Form", model);
        }
        
        TempData["SuccessMessage"] = App.Resources.Views.Admin.ConnectorTypes.Index.ConnectorTypeCreated;
        return RedirectToAction("Index");
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var connectorType = await chargingModuleApi.GetConnectorTypeByIdAsync(id);
        if (connectorType == null)
        {
            return NotFound();
        }

        var model = new AdminConnectorTypeFormViewModel
        {
            ConnectorTypeId = connectorType.ConnectorTypeId,
            NameEn = connectorType.NameEn,
            NameEt = connectorType.NameEt,
            IsActive = connectorType.IsActive
        };

        return View("Form", model);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] AdminConnectorTypeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var updated = await chargingModuleApi.UpdateConnectorTypeAsync(id, model.NameEn, model.NameEt, model.IsActive);
        if (updated == null)
        {
            ModelState.AddModelError("", "Failed to update connector type.");
            return View("Form", model);
        }

        TempData["SuccessMessage"] = App.Resources.Views.Admin.ConnectorTypes.Index.ConnectorTypeUpdated;
        return RedirectToAction("Index");
    }

    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await chargingModuleApi.DeleteConnectorTypeAsync(id);
        if (!deleted)
        {
            TempData["ErrorMessage"] = "Failed to delete connector type.";
            return RedirectToAction("Index");
        }

        TempData["SuccessMessage"] = App.Resources.Views.Admin.ConnectorTypes.Index.ConnectorTypeDeleted;
        return RedirectToAction("Index");
    }
}
