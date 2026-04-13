using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Company;

public class CompanyUserResponse
{
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}

public class AddCompanyUserRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = default!;

    [MaxLength(200)]
    public string? FirstName { get; set; }

    [MaxLength(200)]
    public string? LastName { get; set; }

    [Phone]
    [MaxLength(64)]
    public string? PhoneNumber { get; set; }

    [MinLength(6)]
    [MaxLength(100)]
    public string? Password { get; set; }

    [Compare(nameof(Password))]
    public string? ConfirmPassword { get; set; }

    [Required]
    public int Role { get; set; }
}

public class AddCompanyUserResponse
{
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
    public bool IsExistingUser { get; set; }
    public string AccessStatus { get; set; } = default!;
    public string NextAction { get; set; } = default!;
}

public class UpdateCompanyUserRole
{
    [Required]
    public int Role { get; set; }
}
