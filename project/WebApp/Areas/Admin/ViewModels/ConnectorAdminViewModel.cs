using System;
using System.ComponentModel.DataAnnotations;

namespace WebApp.Areas.Admin.ViewModels;

public class ConnectorAdminViewModel
{
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

