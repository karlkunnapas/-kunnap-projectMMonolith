using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Identity;

public class ChangePasswordRequest
{
    [Required]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = default!;

    [Required]
    [MinLength(6)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = default!;
}

public class DeleteAccountRequest
{
    [DataType(DataType.Password)]
    public string? Password { get; set; }
}

public class TwoFactorSetupResponse
{
    public string SharedKey { get; set; } = default!;
    public string AuthenticatorUri { get; set; } = default!;
    public bool IsTwoFactorEnabled { get; set; }
    public int RecoveryCodesLeft { get; set; }
}

public class EnableTwoFactorRequest
{
    [Required]
    public string VerificationCode { get; set; } = default!;
}

public class TwoFactorStatusResponse
{
    public bool IsTwoFactorEnabled { get; set; }
    public int RecoveryCodesLeft { get; set; }
    public bool HasAuthenticator { get; set; }
}

public class TwoFactorRecoveryCodesResponse
{
    public List<string> RecoveryCodes { get; set; } = new();
}
