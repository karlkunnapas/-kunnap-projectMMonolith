using System.Security.Claims;
using App.DTO.v1.Reservation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;
using WebApp.ApiControllers.v1;

namespace WebApp.Tests.Unit;

public class UnitTestReservationApiController
{
    [Fact]
    public async Task CreateReservation_ReturnsBadRequest_WhenStartTimeIsInPast()
    {
        var chargingApi = new Mock<IChargingModuleApi>(MockBehavior.Strict);
        var companiesApi = new Mock<ICompaniesModuleApi>(MockBehavior.Strict);
        var sut = new ReservationController(chargingApi.Object, companiesApi.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) },
                        "TestAuth"))
                }
            }
        };

        var request = new ReservationCreate
        {
            StationId = Guid.NewGuid(),
            StartTimeUtc = DateTime.UtcNow.AddMinutes(-5),
            EndTimeUtc = DateTime.UtcNow.AddMinutes(30),
            EstimatedEnergyKwh = 10
        };

        var action = await sut.CreateReservation(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var payload = Assert.IsType<App.Dto.v1.Message>(badRequest.Value);
        Assert.Contains("Start time must be in the future.", payload.Messages);
        chargingApi.VerifyNoOtherCalls();
        companiesApi.VerifyNoOtherCalls();
    }
}
