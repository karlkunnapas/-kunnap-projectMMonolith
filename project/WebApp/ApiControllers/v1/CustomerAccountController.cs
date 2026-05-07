using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.Domain.Identity;
using App.DTO.v1.Identity;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Helpers;
using WebApp.Mappers;

namespace WebApp.ApiControllers.v1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customeraccount")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
public class CustomerAccountController : ControllerBase
{
    private const string SettingsJWTPrefix = "JWT";
    private const string SettingsJWTKey = SettingsJWTPrefix + ":Key";
    private const string SettingsJWTIssuer = SettingsJWTPrefix + ":Issuer";
    private const string SettingsJWTAudience = SettingsJWTPrefix + ":Audience";
    private const string SettingsJWTExpiresInSeconds = SettingsJWTPrefix + ":ExpiresInSeconds";
    private const string SettingsJWTRefreshTokenExpiresInSeconds = SettingsJWTPrefix + ":RefreshTokenExpiresInSeconds";

    private readonly IIdentityService _identityService;
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;

    public CustomerAccountController(
        IIdentityService identityService,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IConfiguration configuration,
        AppDbContext context)
    {
        _identityService = identityService;
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _context = context;
    }

    /// <summary>
    /// Register a new customer account.
    /// </summary>
    [HttpPost("register-customer")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(JWTResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JWTResponse>> RegisterCustomer(
        [FromBody] RegisterCustomer request,
        [FromQuery] int? jwtExpiresInSeconds,
        [FromQuery] int? refreshTokenExpiresInSeconds)
    {
        var result = await _identityService.RegisterCustomerAsync(ApiDtoFactory.CreateDto(request));

        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        var appUser = await _userManager.FindByEmailAsync(request.Email);
        if (appUser == null)
        {
            return BadRequest(new Message("User was not found after registration."));
        }

        var token = await GenerateJwtResponseAsync(appUser, jwtExpiresInSeconds, refreshTokenExpiresInSeconds);
        return Ok(token);
    }

    /// <summary>
    /// Register as a company owner and create a company.
    /// </summary>
    [HttpPost("register-company")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(JWTResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JWTResponse>> RegisterCompany(
        [FromBody] RegisterCompanyOwner request,
        [FromQuery] int? jwtExpiresInSeconds,
        [FromQuery] int? refreshTokenExpiresInSeconds)
    {
        var result = await _identityService.RegisterCompanyOwnerAsync(ApiDtoFactory.CreateDto(request));

        if (!result.Success)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        var appUser = await _userManager.FindByEmailAsync(request.Email);
        if (appUser == null)
        {
            return BadRequest(new Message("User was not found after registration."));
        }

        var token = await GenerateJwtResponseAsync(appUser, jwtExpiresInSeconds, refreshTokenExpiresInSeconds);
        return Ok(token);
    }

    /// <summary>
    /// Get all companies the authenticated user belongs to.
    /// </summary>
    [HttpGet("companies")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(UserCompaniesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserCompaniesResponse>> GetCompanies()
    {
        var userId = User.UserId();
        var result = await _identityService.GetUserCompaniesAsync(userId);
        if (!result.Success || result.Data == null)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Message).ToArray()));
        }

        return Ok(ApiDtoFactory.CreateDto(result.Data));
    }

    /// <summary>
    /// Returns true when user has active memberships but all linked companies are deactivated.
    /// </summary>
    [HttpGet("has-deactivated-company-membership")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> HasDeactivatedCompanyMembership()
    {
        var userId = User.UserId();

        var hasDeactivatedMembership = await _context.AppUserCompanies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(uc => uc.Company)
            .AnyAsync(uc =>
                uc.AppUserId == userId
                && uc.IsActive
                && uc.Company != null
                && !uc.Company.IsActive);

        return Ok(hasDeactivatedMembership);
    }

    private async Task<JWTResponse> GenerateJwtResponseAsync(AppUser appUser, int? jwtExpiresInSeconds, int? refreshTokenExpiresInSeconds)
    {
        var claimsPrincipal = await _signInManager.CreateUserPrincipalAsync(appUser);

        if (!_context.Database.ProviderName!.Contains("InMemory"))
        {
            await _context
                .RefreshTokens
                .Where(t => t.UserId == appUser.Id && t.Expiration < DateTime.UtcNow)
                .ExecuteDeleteAsync();
        }

        var refreshToken = new AppRefreshToken
        {
            UserId = appUser.Id,
            Expiration = GetExpirationDateTime(refreshTokenExpiresInSeconds, SettingsJWTRefreshTokenExpiresInSeconds)
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        var jwt = IdentityExtensions.GenerateJwt(
            claimsPrincipal.Claims,
            _configuration.GetValue<string>(SettingsJWTKey)!,
            _configuration.GetValue<string>(SettingsJWTIssuer)!,
            _configuration.GetValue<string>(SettingsJWTAudience)!,
            GetExpirationDateTime(jwtExpiresInSeconds, SettingsJWTExpiresInSeconds));

        return ApiDtoFactory.CreateJwtResponse(jwt, refreshToken.RefreshToken);
    }

    private DateTime GetExpirationDateTime(int? expiresInSeconds, string settingsKey)
    {
        if (expiresInSeconds <= 0)
        {
            expiresInSeconds = int.MaxValue;
        }

        expiresInSeconds = expiresInSeconds < _configuration.GetValue<int>(settingsKey)
            ? expiresInSeconds
            : _configuration.GetValue<int>(settingsKey);

        return DateTime.UtcNow.AddSeconds(expiresInSeconds ?? 60);
    }
}
