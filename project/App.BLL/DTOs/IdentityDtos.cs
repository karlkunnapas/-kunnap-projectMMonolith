using System.ComponentModel.DataAnnotations;
using App.Domain;

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
    public Guid MembershipId { get; set; }
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

public class AddCompanyUserRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(200)]
    public string? FirstName { get; set; }

    [StringLength(200)]
    public string? LastName { get; set; }

    [Phone]
    [StringLength(64)]
    public string? PhoneNumber { get; set; }

    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    public string? ConfirmPassword { get; set; }

    [Required]
    public ECompanyRole Role { get; set; }
}

public class AddCompanyUserResultDto
{
    public Guid CompanyId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public ECompanyRole Role { get; set; }
    public bool IsExistingUser { get; set; }
    public string AccessStatus { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public bool MembershipReactivated { get; set; }
    public bool MembershipAlreadyActive { get; set; }
}

public class CompanyUserMembershipDto
{
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public ECompanyRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}

public class UpdateCompanyUserRoleRequestDto
{
    [Required]
    public ECompanyRole Role { get; set; }
}
