using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.DTO.v1.Identity;
using App.Dto.v1;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Shared.Contracts.Users;
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
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;
    private readonly IUsersModuleApi _usersModuleApi;

    public CustomerAccountController(
        IIdentityService identityService,
        IConfiguration configuration,
        AppDbContext context,
        IUsersModuleApi usersModuleApi)
    {
        _identityService = identityService;
        _configuration = configuration;
        _context = context;
        _usersModuleApi = usersModuleApi;
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

        var userId = await _usersModuleApi.GetUserIdByEmailAsync(request.Email);
        if (userId == null)
        {
            return BadRequest(new Message("User was not found after registration."));
        }

        var token = await GenerateJwtResponseAsync(userId.Value, jwtExpiresInSeconds, refreshTokenExpiresInSeconds);
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

        var userId = await _usersModuleApi.GetUserIdByEmailAsync(request.Email);
        if (userId == null)
        {
            return BadRequest(new Message("User was not found after registration."));
        }

        var token = await GenerateJwtResponseAsync(userId.Value, jwtExpiresInSeconds, refreshTokenExpiresInSeconds);
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

    /// <summary>
    /// Get current authenticated user profile.
    /// </summary>
    [HttpGet("profile")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> GetProfile()
    {
        var userId = User.UserId();
        var profile = await _usersModuleApi.GetUserProfileAsync(userId);
        if (profile == null)
        {
            return NotFound(new Message("User not found."));
        }

        return Ok(new UserProfileResponse
        {
            Email = profile.Email,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            PhoneNumber = profile.PhoneNumber
        });
    }

    /// <summary>
    /// Update current authenticated user profile.
    /// </summary>
    [HttpPut("profile")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfile request)
    {
        var userId = User.UserId();
        var updated = await _usersModuleApi.UpdateUserProfileAsync(userId, new UpdateUserProfileContract
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber
        });

        if (!updated)
        {
            return NotFound(new Message("User not found."));
        }

        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.UserId();
        var result = await _usersModuleApi.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        if (!result.Success)
        {
            if (result.ErrorCode == "NOT_FOUND")
            {
                return NotFound(new Message(result.ErrorMessage ?? "User not found."));
            }

            return BadRequest(new Message(result.ErrorMessage ?? "Password change failed."));
        }

        return NoContent();
    }

    [HttpGet("2fa/status")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(TwoFactorStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TwoFactorStatusResponse>> GetTwoFactorStatus()
    {
        var userId = User.UserId();
        var status = await _usersModuleApi.GetTwoFactorStatusAsync(userId);
        if (!status.UserExists)
        {
            return NotFound(new Message("User not found."));
        }

        return Ok(new TwoFactorStatusResponse
        {
            IsTwoFactorEnabled = status.IsTwoFactorEnabled,
            RecoveryCodesLeft = status.RecoveryCodesLeft,
            HasAuthenticator = status.HasAuthenticator
        });
    }

    [HttpPost("2fa/setup")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(TwoFactorSetupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TwoFactorSetupResponse>> StartTwoFactorSetup()
    {
        var userId = User.UserId();
        var setup = await _usersModuleApi.StartTwoFactorSetupAsync(userId, _configuration["AppName"] ?? "WebApp");
        if (!setup.UserExists)
        {
            return NotFound(new Message("User not found."));
        }

        return Ok(new TwoFactorSetupResponse
        {
            SharedKey = setup.SharedKey,
            AuthenticatorUri = setup.AuthenticatorUri,
            IsTwoFactorEnabled = setup.IsTwoFactorEnabled,
            RecoveryCodesLeft = setup.RecoveryCodesLeft
        });
    }

    [HttpPost("2fa/enable")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(TwoFactorRecoveryCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TwoFactorRecoveryCodesResponse>> EnableTwoFactor([FromBody] EnableTwoFactorRequest request)
    {
        var userId = User.UserId();
        var result = await _usersModuleApi.EnableTwoFactorAsync(userId, request.VerificationCode);
        if (!result.UserExists)
        {
            return NotFound(new Message("User not found."));
        }

        if (!result.Success)
        {
            return BadRequest(new Message(result.ErrorMessage ?? "Two-factor setup failed."));
        }

        return Ok(new TwoFactorRecoveryCodesResponse
        {
            RecoveryCodes = result.RecoveryCodes.ToList()
        });
    }

    [HttpPost("2fa/disable")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableTwoFactor()
    {
        var userId = User.UserId();
        var disabled = await _usersModuleApi.DisableTwoFactorAsync(userId);
        if (!disabled)
        {
            return NotFound(new Message("User not found."));
        }

        return NoContent();
    }

    [HttpPost("2fa/recovery-codes")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(TwoFactorRecoveryCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TwoFactorRecoveryCodesResponse>> RegenerateRecoveryCodes()
    {
        var userId = User.UserId();
        var result = await _usersModuleApi.RegenerateRecoveryCodesAsync(userId);
        if (!result.UserExists)
        {
            return NotFound(new Message("User not found."));
        }

        return Ok(new TwoFactorRecoveryCodesResponse
        {
            RecoveryCodes = result.RecoveryCodes.ToList()
        });
    }

    [HttpPost("delete-account")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        var userId = User.UserId();
        var result = await _usersModuleApi.DeleteAccountAsync(userId, request.Password);
        if (!result.Success && result.ErrorCode == "NOT_FOUND")
        {
            return NotFound(new Message("User not found."));
        }

        if (!result.Success)
        {
            return BadRequest(new Message(result.ErrorMessage ?? "Account deletion failed."));
        }

        return NoContent();
    }

    private async Task<JWTResponse> GenerateJwtResponseAsync(Guid userId, int? jwtExpiresInSeconds, int? refreshTokenExpiresInSeconds)
    {
        var jwtClaims = await _usersModuleApi.GetJwtClaimsAsync(userId);
        var refreshToken = await _usersModuleApi.IssueRefreshTokenAsync(new IssueRefreshTokenContract
        {
            UserId = userId,
            ExpiresAtUtc = GetExpirationDateTime(refreshTokenExpiresInSeconds, SettingsJWTRefreshTokenExpiresInSeconds)
        });

        var jwt = IdentityExtensions.GenerateJwt(
            jwtClaims.Select(c => new Claim(c.Type, c.Value)),
            _configuration.GetValue<string>(SettingsJWTKey)!,
            _configuration.GetValue<string>(SettingsJWTIssuer)!,
            _configuration.GetValue<string>(SettingsJWTAudience)!,
            GetExpirationDateTime(jwtExpiresInSeconds, SettingsJWTExpiresInSeconds));

        return ApiDtoFactory.CreateJwtResponse(jwt, refreshToken ?? string.Empty);
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
