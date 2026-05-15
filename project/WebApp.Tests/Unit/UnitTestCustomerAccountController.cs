using App.DTO.v1.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;
using WebApp.ApiControllers.v1;

namespace WebApp.Tests.Unit;

public class UnitTestCustomerAccountController
{
    [Fact]
    public async Task RegisterCustomer_ReturnsBadRequest_WhenRegistrationFails()
    {
        var companiesApi = new Mock<ICompaniesModuleApi>();
        var usersApi = new Mock<IUsersModuleApi>();
        usersApi
            .Setup(x => x.RegisterCustomerAsync(It.IsAny<RegisterCustomerContract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterCustomerResultContract.Fail("VALIDATION", "Invalid request."));

        var sut = BuildController(companiesApi.Object, usersApi.Object);
        var request = BuildRequest();

        var action = await sut.RegisterCustomer(request, jwtExpiresInSeconds: 60, refreshTokenExpiresInSeconds: 120);
        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var payload = Assert.IsType<App.Dto.v1.Message>(badRequest.Value);
        Assert.Contains("Invalid request.", payload.Messages);
    }

    [Fact]
    public async Task RegisterCustomer_ReturnsJwtResponse_WhenRegistrationSucceeds()
    {
        var companiesApi = new Mock<ICompaniesModuleApi>();
        var usersApi = new Mock<IUsersModuleApi>();
        var userId = Guid.NewGuid();

        usersApi
            .Setup(x => x.RegisterCustomerAsync(It.IsAny<RegisterCustomerContract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisterCustomerResultContract.Ok(userId));
        usersApi
            .Setup(x => x.GetJwtClaimsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JwtClaimContract>
            {
                new() { Type = "sub", Value = userId.ToString() },
                new() { Type = "email", Value = "john@example.com" }
            });
        usersApi
            .Setup(x => x.IssueRefreshTokenAsync(It.IsAny<IssueRefreshTokenContract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh-token");

        var sut = BuildController(companiesApi.Object, usersApi.Object);
        var request = BuildRequest();

        var action = await sut.RegisterCustomer(request, jwtExpiresInSeconds: 60, refreshTokenExpiresInSeconds: 120);
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<JWTResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(payload.JWT));
        Assert.Equal("refresh-token", payload.RefreshToken);
    }

    private static CustomerAccountController BuildController(
        ICompaniesModuleApi companiesApi,
        IUsersModuleApi usersApi)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:Key"] = "this-is-a-long-test-key-for-jwt-signing-1234567890-and-even-longer-abcdef",
                ["JWT:Issuer"] = "test-issuer",
                ["JWT:Audience"] = "test-audience",
                ["JWT:ExpiresInSeconds"] = "3600",
                ["JWT:RefreshTokenExpiresInSeconds"] = "7200",
                ["AppName"] = "WebApp"
            })
            .Build();

        return new CustomerAccountController(configuration, companiesApi, usersApi);
    }

    private static RegisterCustomer BuildRequest() => new()
    {
        FirstName = "John",
        LastName = "Tester",
        Email = "john@example.com",
        PhoneNumber = "+3725000000",
        Password = "Password#123",
        ConfirmPassword = "Password#123"
    };

}
