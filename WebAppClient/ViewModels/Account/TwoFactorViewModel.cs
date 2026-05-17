using System.ComponentModel.DataAnnotations;

namespace WebApp.ViewModels.Account;

public class TwoFactorViewModel
{
    public bool IsTwoFactorEnabled { get; set; }
    public bool HasAuthenticator { get; set; }
    public int RecoveryCodesLeft { get; set; }
    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
    public List<string> RecoveryCodes { get; set; } = new();

    [Display(ResourceType = typeof(App.Resources.Views.Account.Register), Name = nameof(App.Resources.Views.Account.Register.VerificationCode))]
    public string VerificationCode { get; set; } = string.Empty;
}
