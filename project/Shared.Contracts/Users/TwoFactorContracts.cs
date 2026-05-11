namespace Shared.Contracts.Users;

public sealed class TwoFactorStatusContract
{
    public bool UserExists { get; set; }
    public bool IsTwoFactorEnabled { get; set; }
    public int RecoveryCodesLeft { get; set; }
    public bool HasAuthenticator { get; set; }
}

public sealed class TwoFactorSetupContract
{
    public bool UserExists { get; set; }
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
    public bool IsTwoFactorEnabled { get; set; }
    public int RecoveryCodesLeft { get; set; }
}

public sealed class TwoFactorRecoveryCodesContract
{
    public bool UserExists { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyCollection<string> RecoveryCodes { get; set; } = Array.Empty<string>();
}

public sealed class DeleteAccountResultContract
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static DeleteAccountResultContract Ok() => new() { Success = true };
    public static DeleteAccountResultContract Fail(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        ErrorMessage = message
    };
}
