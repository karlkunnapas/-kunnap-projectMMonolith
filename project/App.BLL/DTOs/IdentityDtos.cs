using System.ComponentModel.DataAnnotations;

namespace App.BLL.DTOs;

public class LoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class RegisterCompanyOwnerDto
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
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 1)]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Slug must be lowercase alphanumeric with hyphens only")]
    public string CompanySlug { get; set; } = string.Empty;
}

public class RegisterCustomerDto
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
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class CompanySelectionItemDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanySlug { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class UserCompanyListResultDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public List<CompanySelectionItemDto> Companies { get; set; } = new();
}