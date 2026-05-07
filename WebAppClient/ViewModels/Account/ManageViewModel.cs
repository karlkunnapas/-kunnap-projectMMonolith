using System.ComponentModel.DataAnnotations;

namespace WebApp.ViewModels.Account;

public class ManageViewModel
{
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(64)]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;
}
