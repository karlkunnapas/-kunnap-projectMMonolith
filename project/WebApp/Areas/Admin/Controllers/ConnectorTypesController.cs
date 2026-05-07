using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Admin.ViewModels;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root,SystemAdmin")]
[Route("Admin/[controller]")]
public class ConnectorTypesController(IAdminPanelService adminPanelService) : Controller
{
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? search = null)
    {
        var result = await adminPanelService.GetConnectorTypesAsync(search);
        if (!result.Success || result.Data == null)
        {
            return BadRequest();
        }

        var model = new AdminConnectorTypeListViewModel
        {
            Search = result.Data.Search,
            Items = result.Data.Items.Select(item => new AdminConnectorTypeListItemViewModel
            {
                ConnectorTypeId = item.ConnectorTypeId,
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

        var dto = new AdminConnectorTypeFormDto
        {
            NameEn = model.NameEn,
            NameEt = model.NameEt,
            IsActive = model.IsActive
        };

        var userName = User.Identity?.Name ?? "Unknown";
        var result = await adminPanelService.CreateConnectorTypeAsync(dto, userName);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Errors.FirstOrDefault()?.Message ?? "Failed to create connector type.");
            return View("Form", model);
        }

        TempData["SuccessMessage"] = App.Resources.Views.Admin.ConnectorTypes.Index.ConnectorTypeCreated;
        return RedirectToAction("Index");
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var result = await adminPanelService.GetConnectorTypeAsync(id);
        if (!result.Success || result.Data == null)
        {
            return NotFound();
        }

        var model = new AdminConnectorTypeFormViewModel
        {
            ConnectorTypeId = result.Data.ConnectorTypeId,
            NameEn = result.Data.NameEn,
            NameEt = result.Data.NameEt,
            IsActive = result.Data.IsActive
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

        var dto = new AdminConnectorTypeFormDto
        {
            ConnectorTypeId = id,
            NameEn = model.NameEn,
            NameEt = model.NameEt,
            IsActive = model.IsActive
        };

        var userName = User.Identity?.Name ?? "Unknown";
        var result = await adminPanelService.UpdateConnectorTypeAsync(id, dto, userName);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Errors.FirstOrDefault()?.Message ?? "Failed to update connector type.");
            return View("Form", model);
        }

        TempData["SuccessMessage"] = App.Resources.Views.Admin.ConnectorTypes.Index.ConnectorTypeUpdated;
        return RedirectToAction("Index");
    }

    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userName = User.Identity?.Name ?? "Unknown";
        var result = await adminPanelService.DeleteConnectorTypeAsync(id, userName);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Errors.FirstOrDefault()?.Message ?? "Failed to delete connector type.";
            return RedirectToAction("Index");
        }

        TempData["SuccessMessage"] = App.Resources.Views.Admin.ConnectorTypes.Index.ConnectorTypeDeleted;
        return RedirectToAction("Index");
    }
}
