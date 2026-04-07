using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WebApp.Controllers;
using WebApp.ViewModels;
using Xunit.Abstractions;

namespace WebApp.Tests.Unit;

public class UnitTestHomeController
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly HomeController _homeController;
    private readonly FakeChargingStationService _fakeService;

    public UnitTestHomeController(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        using var logFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = logFactory.CreateLogger<HomeController>();

        _fakeService = new FakeChargingStationService(ServiceResult<HomePageDto>.Ok(new HomePageDto()));
        _homeController = new HomeController(_fakeService, new FakeVehicleService(), logger);
    }

    [Fact]
    public async Task IndexAction_ReturnsHomeVm()
    {
        var result = (await _homeController.Index()) as ViewResult;
        _testOutputHelper.WriteLine(result?.ToString());

        var vm = result?.Model as HomeIndexViewModel;
        Assert.NotNull(vm);
    }

    [Fact]
    public async Task IndexAction_ForwardsFiltersToService()
    {
        await _homeController.Index(status: "Available", connector: "CCS", location: "2.3");

        Assert.NotNull(_fakeService.LastFilters);
        Assert.Equal("Available", _fakeService.LastFilters!.Status);
        Assert.Equal("CCS", _fakeService.LastFilters.Connector);
        Assert.Equal("2.3", _fakeService.LastFilters.Location);
    }

    [Fact]
    public async Task IndexAction_ForwardsVehicleIdToService()
    {
        var vehicleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _homeController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, "Customer")
                    },
                    "TestAuth"))
            }
        };

        await _homeController.Index(vehicleId: vehicleId);

        Assert.NotNull(_fakeService.LastFilters);
        Assert.Equal(vehicleId, _fakeService.LastFilters!.VehicleId);
    }

    private class FakeChargingStationService : IChargingStationService
    {
        private readonly ServiceResult<HomePageDto> _result;

        public HomePageFilterDto? LastFilters { get; private set; }

        public FakeChargingStationService(ServiceResult<HomePageDto> result)
        {
            _result = result;
        }

        public Task<ServiceResult<HomePageDto>> GetHomePageAsync(HomePageFilterDto? filters = null)
        {
            LastFilters = filters;
            return Task.FromResult(_result);
        }
    }

    private class FakeVehicleService : IVehicleService
    {
        public Task<ServiceResult<List<VehicleDto>>> GetUserVehiclesAsync(Guid userId)
            => Task.FromResult(ServiceResult<List<VehicleDto>>.Ok(new List<VehicleDto>()));

        public Task<ServiceResult<VehicleDto>> GetVehicleForUserAsync(Guid id, Guid userId)
            => Task.FromResult(ServiceResult<VehicleDto>.Ok(new VehicleDto { Id = id, Make = "Test", Model = "Model" }));

        public Task<ServiceResult<VehicleDto>> CreateVehicleAsync(Guid userId, VehicleCreateDto dto)
            => Task.FromResult(ServiceResult<VehicleDto>.Fail("NOT_IMPLEMENTED", "Not used in this test."));

        public Task<ServiceResult<VehicleDto>> UpdateVehicleAsync(Guid id, Guid userId, VehicleUpdateDto dto)
            => Task.FromResult(ServiceResult<VehicleDto>.Fail("NOT_IMPLEMENTED", "Not used in this test."));

        public Task<ServiceResult> DeleteVehicleAsync(Guid id, Guid userId)
            => Task.FromResult(ServiceResult.Fail("NOT_IMPLEMENTED", "Not used in this test."));

        public Task<ServiceResult> SetConnectorCompatibilityAsync(Guid vehicleId, Guid userId, IReadOnlyCollection<Guid> connectorIds)
            => Task.FromResult(ServiceResult.Fail("NOT_IMPLEMENTED", "Not used in this test."));

        public Task<ServiceResult<List<VehicleConnectorDto>>> GetCompatibleConnectorsForVehicleAsync(Guid vehicleId, Guid userId)
            => Task.FromResult(ServiceResult<List<VehicleConnectorDto>>.Fail("NOT_IMPLEMENTED", "Not used in this test."));

        public Task<ServiceResult<List<CompatibleStationDto>>> GetCompatibleStationsForVehicleAsync(Guid vehicleId, Guid userId)
            => Task.FromResult(ServiceResult<List<CompatibleStationDto>>.Fail("NOT_IMPLEMENTED", "Not used in this test."));
    }
}