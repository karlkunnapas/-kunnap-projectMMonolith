using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Root.ViewModels;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "Customer")]
public class VehicleController : Controller
{
    private readonly IApiClient _apiClient;

    public VehicleController(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vehicles = await _apiClient.GetAsync<List<VehicleResponseDto>>("api/v1/vehicle");
        var vm = new VehicleListViewModel
        {
            Vehicles = vehicles.Select(v => new VehicleListItemViewModel
            {
                Id = v.Id,
                Make = v.Make,
                Model = v.Model,
                BatteryCapacity = v.BatteryCapacity,
                CompatibleConnectorCount = v.CompatibleConnectors.Count
            }).ToList()
        };
        return View(vm);
    }

    [HttpGet]
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
        if (!ModelState.IsValid)
        {
            await LoadConnectorsAsync(vm, vm.SelectedConnectorIds);
            return View(vm);
        }

        try
        {
            await _apiClient.PostAsync<VehicleResponseDto>("api/v1/vehicle", new VehicleCreateUpdateRequestDto
            {
                Make = vm.Make,
                Model = vm.Model,
                BatteryCapacity = vm.BatteryCapacity,
                ConnectorIds = vm.SelectedConnectorIds
            });
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadConnectorsAsync(vm, vm.SelectedConnectorIds);
            return View(vm);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        try
        {
            var vehicle = await _apiClient.GetAsync<VehicleResponseDto>($"api/v1/vehicle/{id}");
            var vm = new VehicleEditViewModel
            {
                Id = vehicle.Id,
                Make = vehicle.Make,
                Model = vehicle.Model,
                BatteryCapacity = vehicle.BatteryCapacity,
                SelectedConnectorIds = vehicle.CompatibleConnectors.Select(c => c.ConnectorId).ToList()
            };
            await LoadConnectorsAsync(vm, vm.SelectedConnectorIds);
            return View(vm);
        }
        catch (ApiException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, VehicleEditViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await LoadConnectorsAsync(vm, vm.SelectedConnectorIds);
            return View(vm);
        }

        try
        {
            await _apiClient.PutAsync<VehicleResponseDto>($"api/v1/vehicle/{id}", new VehicleCreateUpdateRequestDto
            {
                Make = vm.Make,
                Model = vm.Model,
                BatteryCapacity = vm.BatteryCapacity,
                ConnectorIds = vm.SelectedConnectorIds
            });
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadConnectorsAsync(vm, vm.SelectedConnectorIds);
            return View(vm);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _apiClient.DeleteAsync($"api/v1/vehicle/{id}");
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCompatibility(Guid id, List<Guid> selectedConnectorIds)
    {
        try
        {
            await _apiClient.PutAsync($"api/v1/vehicle/{id}/connectors", selectedConnectorIds);
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch (ApiException)
        {
            return Forbid();
        }
    }

    private async Task LoadConnectorsAsync(VehicleEditViewModel vm, IEnumerable<Guid>? selectedConnectorIds)
    {
        var selected = selectedConnectorIds?.ToHashSet() ?? new HashSet<Guid>();
        var connectors = await _apiClient.GetAsync<List<VehicleConnectorResponseDto>>("api/v1/vehicle/connectors");
        vm.AllConnectors = connectors
            .Select(c => new ConnectorOptionViewModel
            {
                Id = c.ConnectorId,
                Name = c.Name,
                IsSelected = selected.Contains(c.ConnectorId)
            })
            .OrderBy(c => c.Name)
            .ToList();
    }
}
