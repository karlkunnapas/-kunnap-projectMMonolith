using System.ComponentModel.DataAnnotations;

namespace WebApp.ViewModels.Account;

public class CustomerRegisterViewModel
{
    public string? ReturnUrl { get; set; }
    public string? CompanyRegisterUrl { get; set; }

    [Required]
    [StringLength(200)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6, ErrorMessageResourceType = typeof(App.Resources.Views.Account.Register), ErrorMessageResourceName = nameof(App.Resources.Views.Account.Register.PasswordLengthError))]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessageResourceType = typeof(App.Resources.Views.Account.Register), ErrorMessageResourceName = nameof(App.Resources.Views.Account.Register.PasswordMismatchError))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
