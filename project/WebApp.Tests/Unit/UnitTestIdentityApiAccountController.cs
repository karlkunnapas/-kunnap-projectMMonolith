using App.DTO.v1.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts.Users;
using System.Security.Claims;
using WebApp.ApiControllers.Identity;
using WebApp.Helpers;

namespace WebApp.Tests.Unit;

public class UnitTestIdentityApiAccountController
{
    [Fact]
    public async Task Login_ReturnsNotFound_WhenAuthenticationFails()
    {
        var usersApi = new Mock<IUsersModuleApi>();
        usersApi
            .Setup(x => x.AuthenticateByEmailAsync("john@example.com", "wrong-pass", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthenticateUserResultContract.Fail("INVALID_PASSWORD", "Invalid password."));

        var sut = BuildController(usersApi.Object);
        var action = await sut.Login(new Login
        {
            Email = "john@example.com",
            Password = "wrong-pass"
        }, 60, 120);

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }

    [Fact]
    public async Task Login_ReturnsJwtResponse_WhenAuthenticationSucceeds()
    {
        var usersApi = new Mock<IUsersModuleApi>();
        var userId = Guid.NewGuid();
        usersApi
            .Setup(x => x.AuthenticateByEmailAsync("john@example.com", "Password#123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthenticateUserResultContract.Ok(userId));
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

        var sut = BuildController(usersApi.Object);
        var action = await sut.Login(new Login
        {
            Email = "john@example.com",
            Password = "Password#123"
        }, 60, 120);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<JWTResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(payload.JWT));
        Assert.Equal("refresh-token", payload.RefreshToken);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenRegistrationFails()
    {
        var usersApi = new Mock<IUsersModuleApi>();
        usersApi
            .Setup(x => x.RegisterBasicUserAsync(It.IsAny<RegisterBasicUserContract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterBasicUserResultContract
            {
                Success = false,
                Errors = new[] { "User already registered" }
            });

        var sut = BuildController(usersApi.Object);
        var action = await sut.Register(new Register
        {
            Email = "john@example.com",
            Password = "Password#123"
        }, 60, 120);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var payload = Assert.IsType<App.Dto.v1.Message>(badRequest.Value);
        Assert.Contains("User already registered", payload.Messages);
    }

    [Fact]
    public async Task RenewRefreshToken_ReturnsBadRequest_WhenJwtCannotBeParsed()
    {
        var usersApi = new Mock<IUsersModuleApi>();
        var sut = BuildController(usersApi.Object);

        var action = await sut.RenewRefreshToken(new RefreshTokenModel
        {
            Jwt = "not-a-jwt",
            RefreshToken = "r1"
        }, 60, 120);

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task RenewRefreshToken_ReturnsBadRequest_WhenJwtHasNoEmailClaim()
    {
        var usersApi = new Mock<IUsersModuleApi>();
        var sut = BuildController(usersApi.Object);

        var jwt = CreateJwt(Array.Empty<Claim>());
        var action = await sut.RenewRefreshToken(new RefreshTokenModel
        {
            Jwt = jwt,
            RefreshToken = "r1"
        }, 60, 120);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var payload = Assert.IsType<App.Dto.v1.Message>(badRequest.Value);
        Assert.Contains("No email in jwt", payload.Messages);
    }

    [Fact]
    public async Task RenewRefreshToken_ReturnsNotFound_WhenUserCannotBeResolvedByEmail()
    {
        var usersApi = new Mock<IUsersModuleApi>();
        usersApi
            .Setup(x => x.GetUserIdByEmailAsync("john@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var sut = BuildController(usersApi.Object);
        var jwt = CreateJwt(new[] { new Claim(ClaimTypes.Email, "john@example.com") });

        var action = await sut.RenewRefreshToken(new RefreshTokenModel
        {
            Jwt = jwt,
            RefreshToken = "r1"
        }, 60, 120);

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }

    [Fact]
    public async Task RenewRefreshToken_ReturnsJwtResponse_WhenRenewSucceeds()
    {
        var usersApi = new Mock<IUsersModuleApi>();
        var userId = Guid.NewGuid();
        usersApi
            .Setup(x => x.GetUserIdByEmailAsync("john@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
        usersApi
            .Setup(x => x.RenewRefreshTokenAsync(userId, "r1", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RenewRefreshTokenResultContract.Ok("r2"));
        usersApi
            .Setup(x => x.GetJwtClaimsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JwtClaimContract>
            {
                new() { Type = "sub", Value = userId.ToString() },
                new() { Type = "email", Value = "john@example.com" }
            });

        var sut = BuildController(usersApi.Object);
        var jwt = CreateJwt(new[] { new Claim(ClaimTypes.Email, "john@example.com") });

        var action = await sut.RenewRefreshToken(new RefreshTokenModel
        {
            Jwt = jwt,
            RefreshToken = "r1"
        }, 60, 120);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<JWTResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(payload.JWT));
        Assert.Equal("r2", payload.RefreshToken);
    }

    private static AccountController BuildController(IUsersModuleApi usersModuleApi)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:Key"] = "this-is-a-long-test-key-for-jwt-signing-1234567890",
                ["JWT:Issuer"] = "test-issuer",
                ["JWT:Audience"] = "test-audience",
                ["JWT:ExpiresInSeconds"] = "3600",
                ["JWT:RefreshTokenExpiresInSeconds"] = "7200"
            })
            .Build();

        var logger = new Mock<ILogger<AccountController>>();
        return new AccountController(configuration, logger.Object, usersModuleApi);
    }

    private static string CreateJwt(IEnumerable<Claim> claims)
    {
        return IdentityExtensions.GenerateJwt(
            claims,
            "this-is-a-long-test-key-for-jwt-signing-1234567890",
            "test-issuer",
            "test-audience",
            DateTime.UtcNow.AddMinutes(10));
    }
}
