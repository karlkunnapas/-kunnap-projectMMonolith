using System.ComponentModel.DataAnnotations;

namespace WebApp.ViewModels.Account;

public class RegisterViewModel
{
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
    [StringLength(100, MinimumLength = 6, ErrorMessage = "The {0} must be at least {2} characters long.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 1)]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Slug must be lowercase alphanumeric with hyphens only")]
    public string CompanySlug { get; set; } = string.Empty;
}