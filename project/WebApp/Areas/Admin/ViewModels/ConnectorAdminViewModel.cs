using System;
using System.ComponentModel.DataAnnotations;

namespace WebApp.Areas.Admin.ViewModels;

public class ConnectorAdminViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Name (ET)")]
    public string NameEt { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Name (EN)")]
    public string NameEn { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
