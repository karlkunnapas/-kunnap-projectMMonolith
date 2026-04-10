using System.ComponentModel.DataAnnotations;
using App.Domain;

namespace WebApp.Areas.Company.ViewModels;

public class CompanyUserListViewModel
{
    public Guid CompanyId { get; set; }
    public List<CompanyUserItemViewModel> Users { get; set; } = new();
}

public class CompanyUserItemViewModel
{
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public ECompanyRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime JoinedAtUtc { get; set; }
}

public class AddCompanyUserViewModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public ECompanyRole Role { get; set; } = ECompanyRole.Employee;

    [Display(Name = "First name")]
    [StringLength(200)]
    public string? FirstName { get; set; }

    [Display(Name = "Last name")]
    [StringLength(200)]
    public string? LastName { get; set; }

    [Display(Name = "Phone")]
    [Phone]
    [StringLength(64)]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6)]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    public string? ConfirmPassword { get; set; }

    public Guid CompanyId { get; set; }
    public string? ReturnUrl { get; set; }
}

public class AddCompanyUserResultViewModel
{
    public Guid CompanyId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public ECompanyRole Role { get; set; }
    public bool IsExistingUser { get; set; }
    public bool MembershipReactivated { get; set; }
    public bool MembershipAlreadyActive { get; set; }
    public string AccessStatus { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
}

public class EditCompanyUserRoleViewModel
{
    public Guid CompanyId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;

    [Required]
    public ECompanyRole Role { get; set; }
}
