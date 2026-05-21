using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using App.DTO.v1.Identity;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Users;
using WebApp.Helpers;
using WebApp.Mappers;

namespace WebApp.ApiControllers.Identity;

/// <summary>
/// User account controller - login, register, etc.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountController> _logger;
    private readonly Random _random = new Random();
    private readonly IUsersModuleApi _usersModuleApi;

    private const string UserPassProblem = "User/Password problem";
    private const int RandomDelayMin = 500;
    private const int RandomDelayMax = 5000;

    private const string SettingsJWTPrefix = "JWT";
    private const string SettingsJWTKey = SettingsJWTPrefix + ":Key";
    private const string SettingsJWTIssuer = SettingsJWTPrefix + ":Issuer";
    private const string SettingsJWTAudience = SettingsJWTPrefix + ":Audience";
    private const string SettingsJWTExpiresInSeconds = SettingsJWTPrefix + ":ExpiresInSeconds";
    private const string SettingsJWTRefreshTokenExpiresInSeconds = SettingsJWTPrefix + ":RefreshTokenExpiresInSeconds";


    /// <summary>
    /// Constructor
    /// </summary>
    public AccountController(IConfiguration configuration, ILogger<AccountController> logger, IUsersModuleApi usersModuleApi)
    {
        _configuration = configuration;
        _logger = logger;
        _usersModuleApi = usersModuleApi;
    }

    /// <summary>
    /// User authentication, returns JWT and refresh token
    /// </summary>
    /// <param name="loginInfo">Login model</param>
    /// <param name="jwtExpiresInSeconds">Optional, use custom jwt expiration</param>
    /// <param name="refreshTokenExpiresInSeconds">Optional, use custom refresh token expiration</param>
    /// <returns>JWT and refresh token</returns>
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(JWTResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(App.Dto.v1.Message), StatusCodes.Status404NotFound)]
    [HttpPost]
    public async Task<ActionResult<JWTResponse>> Login(
        [FromBody]
        Login loginInfo,
        [FromQuery]
        int? jwtExpiresInSeconds,
        [FromQuery]
        int? refreshTokenExpiresInSeconds
    )
    {
        var authResult = await _usersModuleApi.AuthenticateByEmailAsync(loginInfo.Email, loginInfo.Password);
        if (!authResult.Success)
        {
            _logger.LogWarning("WebApi login failed, email {} not found", loginInfo.Email);
            await Task.Delay(_random.Next(RandomDelayMin, RandomDelayMax));
            return NotFound(new App.Dto.v1.Message(UserPassProblem));
        }
        var refreshToken = await _usersModuleApi.IssueRefreshTokenAsync(new IssueRefreshTokenContract
        {
            UserId = authResult.UserId,
            ExpiresAtUtc = GetExpirationDateTime(refreshTokenExpiresInSeconds, SettingsJWTRefreshTokenExpiresInSeconds)
        });

        var jwtClaims = await _usersModuleApi.GetJwtClaimsAsync(authResult.UserId);

        var jwt = IdentityExtensions.GenerateJwt(
            jwtClaims.Select(c => new Claim(c.Type, c.Value)),
            _configuration.GetValue<string>(SettingsJWTKey)!,
            _configuration.GetValue<string>(SettingsJWTIssuer)!,
            _configuration.GetValue<string>(SettingsJWTAudience)!,
            GetExpirationDateTime(jwtExpiresInSeconds, SettingsJWTExpiresInSeconds)
        );

        var responseData = ApiDtoFactory.CreateJwtResponse(jwt, refreshToken ?? string.Empty);

        return Ok(responseData);
    }


    /// <summary>
    /// Register new user, returns JWT and refresh token
    /// </summary>
    /// <param name="registerModel">Reg info</param>
    /// <param name="jwtExpiresInSeconds">Optional custom jwt expiration</param>
    /// <param name="refreshTokenExpiresInSeconds">Optional custom refresh token expiration</param>
    /// <returns></returns>
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(JWTResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(App.Dto.v1.Message), StatusCodes.Status400BadRequest)]
    [HttpPost]
    public async Task<ActionResult<JWTResponse>> Register(
        [FromBody]
        Register registerModel,
        [FromQuery]
        int? jwtExpiresInSeconds,
        [FromQuery]
        int? refreshTokenExpiresInSeconds
    )
    {
        var registerResult = await _usersModuleApi.RegisterBasicUserAsync(new RegisterBasicUserContract
        {
            Email = registerModel.Email,
            Password = registerModel.Password
        });

        if (registerResult.Success)
        {
            _logger.LogInformation("User {Email} created a new account with password", registerModel.Email);

            var jwtClaims = await _usersModuleApi.GetJwtClaimsAsync(registerResult.UserId);
            var jwt = IdentityExtensions.GenerateJwt(
                jwtClaims.Select(c => new Claim(c.Type, c.Value)),
                _configuration.GetValue<string>(SettingsJWTKey)!,
                _configuration.GetValue<string>(SettingsJWTIssuer)!,
                _configuration.GetValue<string>(SettingsJWTAudience)!,
                GetExpirationDateTime(jwtExpiresInSeconds, SettingsJWTExpiresInSeconds)
            );

            var refreshToken = await _usersModuleApi.IssueRefreshTokenAsync(new IssueRefreshTokenContract
            {
                UserId = registerResult.UserId,
                ExpiresAtUtc = GetExpirationDateTime(refreshTokenExpiresInSeconds, SettingsJWTRefreshTokenExpiresInSeconds)
            });

            _logger.LogInformation("WebApi login. User {User}", registerModel.Email);
            return Ok(ApiDtoFactory.CreateJwtResponse(jwt, refreshToken ?? string.Empty));
        }

        return BadRequest(new App.Dto.v1.Message() { Messages = registerResult.Errors.ToList() });
    }

    /// <summary>
    /// Renew JWT using refresh token
    /// </summary>
    /// <param name="refreshTokenModel">Data for renewal</param>
    /// <param name="jwtExpiresInSeconds">Optional custom expiration for jwt</param>
    /// <param name="refreshTokenExpiresInSeconds">Optional custom expiration for refresh token</param>
    /// <returns></returns>
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(JWTResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(App.Dto.v1.Message), StatusCodes.Status400BadRequest)]
    [HttpPost]
    public async Task<ActionResult<JWTResponse>> RenewRefreshToken(
        [FromBody]
        RefreshTokenModel refreshTokenModel,
        [FromQuery]
        int? jwtExpiresInSeconds,
        [FromQuery]
        int? refreshTokenExpiresInSeconds
    )
    {
        var normalizedJwt = StripBearerPrefix(refreshTokenModel.Jwt);
        var normalizedRefreshToken = refreshTokenModel.RefreshToken?.Trim() ?? string.Empty;

        JwtSecurityToken jwtToken;
        // get user info from jwt
        try
        {
            jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(normalizedJwt);
            if (jwtToken == null)
            {
                return BadRequest(new App.Dto.v1.Message("No token"));
            }
        }
        catch (Exception e)
        {
            return BadRequest(new App.Dto.v1.Message($"Cant parse the token, {e.Message}"));
        }

        // validate jwt, ignore expiration date
        if (!IdentityExtensions.ValidateJwt(
                normalizedJwt,
                _configuration.GetValue<string>(SettingsJWTKey)!,
                _configuration.GetValue<string>(SettingsJWTIssuer)!,
                _configuration.GetValue<string>(SettingsJWTAudience)!
            ))
        {
            return BadRequest("JWT validation fail");
        }

        var userEmail = jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
        if (userEmail == null)
        {
            return BadRequest(new App.Dto.v1.Message("No email in jwt"));
        }

        var userId = await _usersModuleApi.GetUserIdByEmailAsync(userEmail);
        if (userId == null)
        {
            return NotFound($"User with email {userEmail} not found");
        }


        var renewResult = await _usersModuleApi.RenewRefreshTokenAsync(
            userId.Value,
            normalizedRefreshToken,
            GetExpirationDateTime(refreshTokenExpiresInSeconds, SettingsJWTRefreshTokenExpiresInSeconds));

        if (!renewResult.Success)
        {
            return BadRequest(new App.Dto.v1.Message(renewResult.ErrorMessage ?? "Refresh token renewal failed."));
        }

        // generate new jwt
        var jwtClaims = await _usersModuleApi.GetJwtClaimsAsync(userId.Value);

        // generate jwt
        var jwt = IdentityExtensions.GenerateJwt(
            jwtClaims.Select(c => new Claim(c.Type, c.Value)),
            _configuration.GetValue<string>(SettingsJWTKey)!,
            _configuration.GetValue<string>(SettingsJWTIssuer)!,
            _configuration.GetValue<string>(SettingsJWTAudience)!,
            GetExpirationDateTime(jwtExpiresInSeconds, SettingsJWTExpiresInSeconds)
        );

        var res = ApiDtoFactory.CreateJwtResponse(jwt, renewResult.RefreshToken ?? string.Empty);

        return Ok(res);
    }

    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(App.Dto.v1.Message), StatusCodes.Status404NotFound)]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost]
    public async Task<ActionResult> Logout([FromBody] LogoutInfo logout)
    {
        // delete the refresh token - so user is kicked out after jwt expiration
        // We do not invalidate the jwt on serverside - that would require pipeline modification and checking against db on every request
        // so client can actually continue to use the jwt until it expires (keep the jwt expiration time short ~1 min)

        var userId = User.UserId();
        var deleteCount = await _usersModuleApi.RevokeRefreshTokenAsync(userId, logout.RefreshToken);

        return Ok(new { TokenDeleteCount = deleteCount });
    }

    private DateTime GetExpirationDateTime(int? expiresInSeconds, string settingsKey)
    {
        if (expiresInSeconds <= 0) expiresInSeconds = int.MaxValue;
        expiresInSeconds = expiresInSeconds < _configuration.GetValue<int>(settingsKey)
            ? expiresInSeconds
            : _configuration.GetValue<int>(settingsKey);

        return DateTime.UtcNow.AddSeconds(expiresInSeconds ?? 60);
    }

    private static string StripBearerPrefix(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        const string bearer = "Bearer ";
        var normalized = token.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)
            ? token[bearer.Length..].Trim()
            : token.Trim();

        return normalized.Trim('"').Trim('\'');
    }
}
