using System;
using System.ComponentModel.DataAnnotations;

namespace WebApp.Areas.Admin.ViewModels;

public class CompanyAdminViewModel
{
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [StringLength(128, MinimumLength = 1)]
    public string ContactEmail { get; set; } = string.Empty;

    [StringLength(128, MinimumLength = 1)]
    public string ContactPhone { get; set; } = string.Empty;

    [StringLength(128, MinimumLength = 1)]
    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

