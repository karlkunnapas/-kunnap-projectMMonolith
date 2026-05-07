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
using System.Text.Encodings.Web;
using System.Text;
using System.Security.Claims;
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
    private readonly UrlEncoder _urlEncoder;

    public CustomerAccountController(
        IIdentityService identityService,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IConfiguration configuration,
        AppDbContext context,
        UrlEncoder urlEncoder)
    {
        _identityService = identityService;
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _context = context;
        _urlEncoder = urlEncoder;
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
        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            return NotFound(new Message("User not found."));
        }

        var claims = await _userManager.GetClaimsAsync(appUser);
        var firstName = claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value ?? string.Empty;
        var lastName = claims.FirstOrDefault(c => c.Type == ClaimTypes.Surname)?.Value ?? string.Empty;

        return Ok(new UserProfileResponse
        {
            Email = appUser.Email ?? string.Empty,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = appUser.PhoneNumber ?? string.Empty
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
        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            return NotFound(new Message("User not found."));
        }

        appUser.PhoneNumber = request.PhoneNumber.Trim();
        var updateUserResult = await _userManager.UpdateAsync(appUser);
        if (!updateUserResult.Succeeded)
        {
            return BadRequest(new Message(updateUserResult.Errors.Select(e => e.Description).ToArray()));
        }

        var claims = await _userManager.GetClaimsAsync(appUser);
        var givenNameClaims = claims.Where(c => c.Type == ClaimTypes.GivenName).ToList();
        var surnameClaims = claims.Where(c => c.Type == ClaimTypes.Surname).ToList();

        if (givenNameClaims.Count != 0)
        {
            var removeGivenNameResult = await _userManager.RemoveClaimsAsync(appUser, givenNameClaims);
            if (!removeGivenNameResult.Succeeded)
            {
                return BadRequest(new Message(removeGivenNameResult.Errors.Select(e => e.Description).ToArray()));
            }
        }

        if (surnameClaims.Count != 0)
        {
            var removeSurnameResult = await _userManager.RemoveClaimsAsync(appUser, surnameClaims);
            if (!removeSurnameResult.Succeeded)
            {
                return BadRequest(new Message(removeSurnameResult.Errors.Select(e => e.Description).ToArray()));
            }
        }

        var addClaimsResult = await _userManager.AddClaimsAsync(appUser, new[]
        {
            new Claim(ClaimTypes.GivenName, request.FirstName.Trim()),
            new Claim(ClaimTypes.Surname, request.LastName.Trim())
        });

        if (!addClaimsResult.Succeeded)
        {
            return BadRequest(new Message(addClaimsResult.Errors.Select(e => e.Description).ToArray()));
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
        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            return NotFound(new Message("User not found."));
        }

        var result = await _userManager.ChangePasswordAsync(appUser, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Description).ToArray()));
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
        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            return NotFound(new Message("User not found."));
        }

        var recoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(appUser);
        var hasAuthenticator = !string.IsNullOrWhiteSpace(await _userManager.GetAuthenticatorKeyAsync(appUser));

        return Ok(new TwoFactorStatusResponse
        {
            IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(appUser),
            RecoveryCodesLeft = recoveryCodesLeft,
            HasAuthenticator = hasAuthenticator
        });
    }

    [HttpPost("2fa/setup")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(TwoFactorSetupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TwoFactorSetupResponse>> StartTwoFactorSetup()
    {
        var userId = User.UserId();
        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            return NotFound(new Message("User not found."));
        }

        var key = await _userManager.GetAuthenticatorKeyAsync(appUser);
        if (string.IsNullOrWhiteSpace(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(appUser);
            key = await _userManager.GetAuthenticatorKeyAsync(appUser);
        }

        key ??= string.Empty;
        var email = await _userManager.GetEmailAsync(appUser) ?? appUser.UserName ?? "user";
        var appName = _configuration["AppName"] ?? "WebApp";
        var recoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(appUser);

        return Ok(new TwoFactorSetupResponse
        {
            SharedKey = FormatKey(key),
            AuthenticatorUri = GenerateQrCodeUri(appName, email, key),
            IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(appUser),
            RecoveryCodesLeft = recoveryCodesLeft
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
        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            return NotFound(new Message("User not found."));
        }

        var verificationCode = request.VerificationCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        var is2FaTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
            appUser,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            verificationCode);

        if (!is2FaTokenValid)
        {
            return BadRequest(new Message("Verification code is invalid."));
        }

        var setResult = await _userManager.SetTwoFactorEnabledAsync(appUser, true);
        if (!setResult.Succeeded)
        {
            return BadRequest(new Message(setResult.Errors.Select(e => e.Description).ToArray()));
        }

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(appUser, 10);
        return Ok(new TwoFactorRecoveryCodesResponse
        {
            RecoveryCodes = (recoveryCodes ?? Enumerable.Empty<string>()).ToList()
        });
    }

    [HttpPost("2fa/disable")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableTwoFactor()
    {
        var userId = User.UserId();
        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            return NotFound(new Message("User not found."));
        }

        await _userManager.SetTwoFactorEnabledAsync(appUser, false);
        return NoContent();
    }

    [HttpPost("2fa/recovery-codes")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(TwoFactorRecoveryCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Message), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TwoFactorRecoveryCodesResponse>> RegenerateRecoveryCodes()
    {
        var userId = User.UserId();
        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            return NotFound(new Message("User not found."));
        }

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(appUser, 10);
        return Ok(new TwoFactorRecoveryCodesResponse
        {
            RecoveryCodes = (recoveryCodes ?? Enumerable.Empty<string>()).ToList()
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
        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            return NotFound(new Message("User not found."));
        }

        var hasPassword = await _userManager.HasPasswordAsync(appUser);
        if (hasPassword)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new Message("Password is required."));
            }

            var validPassword = await _userManager.CheckPasswordAsync(appUser, request.Password);
            if (!validPassword)
            {
                return BadRequest(new Message("Incorrect password."));
            }
        }

        var result = await _userManager.DeleteAsync(appUser);
        if (!result.Succeeded)
        {
            return BadRequest(new Message(result.Errors.Select(e => e.Description).ToArray()));
        }

        return NoContent();
    }

    private string GenerateQrCodeUri(string appName, string email, string unformattedKey)
    {
        return string.Format(
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            _urlEncoder.Encode(appName),
            _urlEncoder.Encode(email),
            unformattedKey);
    }

    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
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
