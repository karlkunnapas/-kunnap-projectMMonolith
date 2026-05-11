namespace Shared.Contracts.Users;

public sealed class RenewRefreshTokenResultContract
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RefreshToken { get; set; }

    public static RenewRefreshTokenResultContract Ok(string refreshToken) => new()
    {
        Success = true,
        RefreshToken = refreshToken
    };

    public static RenewRefreshTokenResultContract Fail(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        ErrorMessage = message
    };
}
