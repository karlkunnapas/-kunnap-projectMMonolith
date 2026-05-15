using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts.Charging;
using Shared.Contracts.Users;
using WebApp.Controllers;
using WebApp.ViewModels;
using Xunit.Abstractions;

namespace WebApp.Tests.Unit;

public class UnitTestHomeController
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly HomeController _homeController;
    private readonly Mock<IChargingModuleApi> _chargingModuleApiMock = new();
    private readonly Mock<IUsersModuleApi> _usersModuleApiMock = new();

    public UnitTestHomeController(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        using var logFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = logFactory.CreateLogger<HomeController>();

        _chargingModuleApiMock
            .Setup(x => x.GetStationsForHomeAsync(It.IsAny<EStationStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChargingStationContract>());

        _usersModuleApiMock
            .Setup(x => x.GetUserVehiclesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserVehicleContract>());
        _usersModuleApiMock
            .Setup(x => x.GetVehicleConnectorIdsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        _homeController = new HomeController(_chargingModuleApiMock.Object, _usersModuleApiMock.Object, logger);
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
    public async Task IndexAction_ForwardsStatusToChargingApi()
    {
        await _homeController.Index(status: "Available", connector: "CCS", location: "2.3");

        _chargingModuleApiMock.Verify(
            x => x.GetStationsForHomeAsync(EStationStatus.Available, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IndexAction_WithCustomerVehicle_ResolvesOwnership()
    {
        var vehicleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _usersModuleApiMock
            .Setup(x => x.GetVehicleForUserAsync(vehicleId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVehicleContract
            {
                VehicleId = vehicleId,
                UserId = userId,
                Make = "Test",
                Model = "Model"
            });

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

        _usersModuleApiMock.Verify(
            x => x.GetVehicleForUserAsync(vehicleId, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
