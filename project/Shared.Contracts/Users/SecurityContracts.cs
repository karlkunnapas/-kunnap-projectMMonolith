namespace Shared.Contracts.Users;

public sealed class ChangePasswordResultContract
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static ChangePasswordResultContract Ok() => new() { Success = true };

    public static ChangePasswordResultContract Fail(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        ErrorMessage = message
    };
}
