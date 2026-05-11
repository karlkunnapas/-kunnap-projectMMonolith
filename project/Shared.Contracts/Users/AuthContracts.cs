namespace Shared.Contracts.Users;

public sealed class AuthenticateUserResultContract
{
    public bool Success { get; set; }
    public Guid UserId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static AuthenticateUserResultContract Ok(Guid userId) => new()
    {
        Success = true,
        UserId = userId
    };

    public static AuthenticateUserResultContract Fail(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        ErrorMessage = message
    };
}

public sealed class RegisterBasicUserContract
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterBasicUserResultContract
{
    public bool Success { get; set; }
    public Guid UserId { get; set; }
    public IReadOnlyCollection<string> Errors { get; set; } = Array.Empty<string>();
}

public sealed class JwtClaimContract
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
