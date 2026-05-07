using System.ComponentModel.DataAnnotations;

namespace App.DTO.v1.Identity;

public class UserProfileResponse
{
    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string FirstName { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string LastName { get; set; } = default!;

    [Required]
    [Phone]
    [MaxLength(64)]
    public string PhoneNumber { get; set; } = default!;
}

public class UpdateUserProfile
{
    [Required]
    [MaxLength(200)]
    public string FirstName { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string LastName { get; set; } = default!;

    [Required]
    [Phone]
    [MaxLength(64)]
    public string PhoneNumber { get; set; } = default!;
}
