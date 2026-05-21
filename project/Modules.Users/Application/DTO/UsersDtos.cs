namespace Modules.Users.Application.DTO;

internal sealed class CreateUserVehicleDto
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public IReadOnlyCollection<Guid> ConnectorIds { get; set; } = Array.Empty<Guid>();
}

internal sealed class UpdateUserVehicleDto
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public IReadOnlyCollection<Guid> ConnectorIds { get; set; } = Array.Empty<Guid>();
}

internal sealed class UserVehicleDto
{
    public Guid VehicleId { get; set; }
    public Guid UserId { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal? BatteryCapacity { get; set; }
    public IReadOnlyCollection<Guid> ConnectorIds { get; set; } = Array.Empty<Guid>();
}

internal sealed class UserProfileDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

internal sealed class UpdateUserProfileDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

internal sealed class ChangePasswordResultDto
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static ChangePasswordResultDto Ok()
    {
        return new()
        {
            Success = true
        };
    }

    public static ChangePasswordResultDto Fail(string code, string message)
    {
        return new()
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message
        };
    }
}

internal sealed class IssueRefreshTokenDto
{
    public Guid UserId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

internal sealed class RenewRefreshTokenResultDto
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RefreshToken { get; set; }

    public static RenewRefreshTokenResultDto Ok(string refreshToken)
    {
        return new()
        {
            Success = true,
            RefreshToken = refreshToken
        };
    }

    public static RenewRefreshTokenResultDto Fail(string code, string message)
    {
        return new()
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message
        };
    }
}

internal sealed class RegisterCustomerDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

internal sealed class RegisterCustomerResultDto
{
    public bool Success { get; set; }
    public Guid UserId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static RegisterCustomerResultDto Ok(Guid userId)
    {
        return new()
        {
            Success = true,
            UserId = userId
        };
    }

    public static RegisterCustomerResultDto Fail(string code, string message)
    {
        return new()
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message
        };
    }
}

internal sealed class TwoFactorStatusDto
{
    public bool UserExists { get; set; }
    public bool IsTwoFactorEnabled { get; set; }
    public int RecoveryCodesLeft { get; set; }
    public bool HasAuthenticator { get; set; }
}

internal sealed class TwoFactorSetupDto
{
    public bool UserExists { get; set; }
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
    public bool IsTwoFactorEnabled { get; set; }
    public int RecoveryCodesLeft { get; set; }
}

internal sealed class TwoFactorRecoveryCodesDto
{
    public bool UserExists { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyCollection<string> RecoveryCodes { get; set; } = Array.Empty<string>();
}

internal sealed class DeleteAccountResultDto
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static DeleteAccountResultDto Ok()
    {
        return new()
        {
            Success = true
        };
    }

    public static DeleteAccountResultDto Fail(string code, string message)
    {
        return new()
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message
        };
    }
}

internal sealed class AuthenticateUserResultDto
{
    public bool Success { get; set; }
    public Guid UserId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static AuthenticateUserResultDto Ok(Guid userId)
    {
        return new()
        {
            Success = true,
            UserId = userId
        };
    }

    public static AuthenticateUserResultDto Fail(string code, string message)
    {
        return new()
        {
            Success = false,
            ErrorCode = code,
            ErrorMessage = message
        };
    }
}

internal sealed class RegisterBasicUserDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

internal sealed class RegisterBasicUserResultDto
{
    public bool Success { get; set; }
    public Guid UserId { get; set; }
    public IReadOnlyCollection<string> Errors { get; set; } = Array.Empty<string>();
}

internal sealed class JwtClaimDto
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
