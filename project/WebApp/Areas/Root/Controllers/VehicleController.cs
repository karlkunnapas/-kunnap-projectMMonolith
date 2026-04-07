using System.Security.Claims;
using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer")]
public class VehicleController : Controller
{
    private readonly IVehicleService _vehicleService;
    private readonly IUnitOfWork _unitOfWork;

    public VehicleController(IVehicleService vehicleService, IUnitOfWork unitOfWork)
    {
        _vehicleService = vehicleService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _vehicleService.GetUserVehiclesAsync(userId.Value);
        var vm = new VehicleListViewModel
        {
            Vehicles = result.Data?.Select(v => new VehicleListItemViewModel
            {
                Id = v.Id,
                Make = v.Make,
                Model = v.Model,
                BatteryCapacity = v.BatteryCapacity,
                CompatibleConnectorCount = v.CompatibleConnectors.Count
            }).ToList() ?? new List<VehicleListItemViewModel>()
        };

        return View(vm);
    }

    public async Task<IActionResult> Create()
    {
        var vm = new VehicleEditViewModel();
        await LoadConnectorsAsync(vm, null);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleEditViewModel vm)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await LoadConnectorsAsync(vm, vm.SelectedConnectorIds);
            return View(vm);
        }

        var result = await _vehicleService.CreateVehicleAsync(userId.Value, new VehicleCreateDto
        {
            Make = vm.Make,
            Model = vm.Model,
            BatteryCapacity = vm.BatteryCapacity,
            ConnectorIds = vm.SelectedConnectorIds
        });

        if (!result.Success)
        {
            AddErrors(result.Errors);
            await LoadConnectorsAsync(vm, vm.SelectedConnectorIds);
            return View(vm);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _vehicleService.GetVehicleForUserAsync(id, userId.Value);
        if (!result.Success || result.Data == null)
        {
            return Forbid();
        }

        var vm = new VehicleEditViewModel
        {
            Id = result.Data.Id,
            Make = result.Data.Make,
            Model = result.Data.Model,
            BatteryCapacity = result.Data.BatteryCapacity,
            SelectedConnectorIds = result.Data.CompatibleConnectors.Select(c => c.ConnectorId).ToList()
        };

        await LoadConnectorsAsync(vm, vm.SelectedConnectorIds);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, VehicleEditViewModel vm)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await LoadConnectorsAsync(vm, vm.SelectedConnectorIds);
            return View(vm);
        }

        var result = await _vehicleService.UpdateVehicleAsync(id, userId.Value, new VehicleUpdateDto
        {
            Make = vm.Make,
            Model = vm.Model,
            BatteryCapacity = vm.BatteryCapacity,
            ConnectorIds = vm.SelectedConnectorIds
        });

        if (!result.Success)
        {
            if (result.Errors.Any(e => e.Code == "FORBIDDEN"))
            {
                return Forbid();
            }

            AddErrors(result.Errors);
            await LoadConnectorsAsync(vm, vm.SelectedConnectorIds);
            return View(vm);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _vehicleService.DeleteVehicleAsync(id, userId.Value);
        if (!result.Success && result.Errors.Any(e => e.Code == "FORBIDDEN"))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCompatibility(Guid id, List<Guid> selectedConnectorIds)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var result = await _vehicleService.SetConnectorCompatibilityAsync(id, userId.Value, selectedConnectorIds);
        if (!result.Success && result.Errors.Any(e => e.Code == "FORBIDDEN"))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task LoadConnectorsAsync(VehicleEditViewModel vm, IEnumerable<Guid>? selectedConnectorIds)
    {
        var selected = selectedConnectorIds?.ToHashSet() ?? new HashSet<Guid>();

        var connectors = await _unitOfWork.Connectors
            .GetQueryable()
            .Where(c => c.IsActive)
            .ToListAsync();

        vm.AllConnectors = connectors
            .Select(c => new ConnectorOptionViewModel
            {
                Id = c.Id,
                Name = c.Name.Translate() ?? c.Name.ToString() ?? string.Empty,
                IsSelected = selected.Contains(c.Id)
            })
            .OrderBy(c => c.Name)
            .ToList();
    }

    private void AddErrors(IEnumerable<ServiceError> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error.Message);
        }
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}

