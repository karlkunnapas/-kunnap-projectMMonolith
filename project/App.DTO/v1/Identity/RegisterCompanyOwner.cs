using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Identity;

/// <summary>
/// Company owner registration request.
/// </summary>
public class RegisterCompanyOwner
{
    [Required]
    [MaxLength(200)]
    public string FirstName { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string LastName { get; set; } = default!;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = default!;

    [Required]
    [Phone]
    [MaxLength(64)]
    public string PhoneNumber { get; set; } = default!;

    [Required]
    [MinLength(6)]
    [MaxLength(100)]
    public string Password { get; set; } = default!;

    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = default!;

    [Required]
    [MaxLength(128)]
    public string CompanyName { get; set; } = default!;

    [Required]
    [MaxLength(128)]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Slug must be lowercase alphanumeric with hyphens only")]
    public string CompanySlug { get; set; } = default!;
}
