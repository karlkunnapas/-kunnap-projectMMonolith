using System.ComponentModel.DataAnnotations;

namespace WebApp.ViewModels.Account;

public class ManageViewModel
{
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(64)]
    public string PhoneNumber { get; set; } = string.Empty;
}
