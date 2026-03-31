using System.Threading.Tasks;
using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebApp.Controllers;
using WebApp.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace WebApp.Tests.Unit;

public class UnitTestHomeController
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly HomeController _homeController;

    public UnitTestHomeController(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        using var logFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = logFactory.CreateLogger<HomeController>();

        var fakeService = new FakeChargingStationService(ServiceResult<HomePageDto>.Ok(new HomePageDto()));
        _homeController = new HomeController(fakeService, logger);
    }

    [Fact]
    public async Task IndexAction_ReturnsHomeVm()
    {
        var result = (await _homeController.Index()) as ViewResult;
        _testOutputHelper.WriteLine(result?.ToString());

        var vm = result?.Model as HomeIndexViewModel;
        Assert.NotNull(vm);
    }

    private class FakeChargingStationService : IChargingStationService
    {
        private readonly ServiceResult<HomePageDto> _result;

        public FakeChargingStationService(ServiceResult<HomePageDto> result)
        {
            _result = result;
        }

        public Task<ServiceResult<HomePageDto>> GetHomePageAsync()
        {
            return Task.FromResult(_result);
        }
    }
}