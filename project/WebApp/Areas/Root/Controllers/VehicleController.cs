using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Charging;
using Shared.Contracts.Users;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer")]
public class VehicleController : Controller
{
    private readonly IUsersModuleApi _usersModuleApi;
    private readonly IChargingModuleApi _chargingModuleApi;
    private static string R(string key) => App.Resources.Views.Shared._Layout.ResourceManager.GetString(key) ?? key;

    public VehicleController(IUsersModuleApi usersModuleApi, IChargingModuleApi chargingModuleApi)
    {
        _usersModuleApi = usersModuleApi;
        _chargingModuleApi = chargingModuleApi;
    }

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        var vehicles = await _usersModuleApi.GetUserVehiclesAsync(userId.Value);
        var vm = new VehicleListViewModel
        {
            Vehicles = vehicles.Select(v => new VehicleListItemViewModel
            {
                Id = v.VehicleId,
                Make = v.Make,
                Model = v.Model,
                BatteryCapacity = v.BatteryCapacity,
                CompatibleConnectorCount = v.ConnectorIds.Count
            }).ToList()
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

        try
        {
            await _usersModuleApi.CreateVehicleAsync(
                userId.Value,
                new CreateUserVehicleContract
                {
                    Make = vm.Make,
                    Model = vm.Model,
                    BatteryCapacity = vm.BatteryCapacity,
                    ConnectorIds = vm.SelectedConnectorIds
                });
        }
        catch
        {
            ModelState.AddModelError(string.Empty, R("UnableToCreateVehicle"));
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

        var vehicle = await _usersModuleApi.GetVehicleForUserAsync(id, userId.Value);
        if (vehicle == null)
        {
            return Forbid();
        }

        var vm = new VehicleEditViewModel
        {
            Id = vehicle.VehicleId,
            Make = vehicle.Make,
            Model = vehicle.Model,
            BatteryCapacity = vehicle.BatteryCapacity,
            SelectedConnectorIds = vehicle.ConnectorIds.ToList()
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

        var result = await _usersModuleApi.UpdateVehicleAsync(
            id,
            userId.Value,
            new UpdateUserVehicleContract
            {
                Make = vm.Make,
                Model = vm.Model,
                BatteryCapacity = vm.BatteryCapacity,
                ConnectorIds = vm.SelectedConnectorIds
            });

        if (result == null)
        {
            ModelState.AddModelError(string.Empty, R("UnableToUpdateVehicle"));
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

        var deleted = await _usersModuleApi.DeleteVehicleAsync(id, userId.Value);
        if (!deleted)
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

        var updated = await _usersModuleApi.SetConnectorCompatibilityAsync(id, userId.Value, selectedConnectorIds);
        if (!updated)
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    private async Task LoadConnectorsAsync(VehicleEditViewModel vm, IEnumerable<Guid>? selectedConnectorIds)
    {
        var selected = selectedConnectorIds?.ToHashSet() ?? new HashSet<Guid>();

        var connectors = await _chargingModuleApi.GetConnectorsAsync(includeInactive: false);

        vm.AllConnectors = connectors
            .Select(c => new ConnectorOptionViewModel
            {
                Id = c.Id,
                Name = c.Name,
                IsSelected = selected.Contains(c.Id)
            })
            .OrderBy(c => c.Name)
            .ToList();
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
