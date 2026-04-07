using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
        _homeController = new HomeController(_fakeService, logger);
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
}