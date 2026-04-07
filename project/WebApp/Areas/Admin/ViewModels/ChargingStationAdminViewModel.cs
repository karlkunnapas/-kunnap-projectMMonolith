using System;
using System.ComponentModel.DataAnnotations;
using App.Domain;

namespace WebApp.Areas.Admin.ViewModels;

public class ChargingStationAdminViewModel
{
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [StringLength(128, MinimumLength = 1)]
    public string Location { get; set; } = string.Empty;

    public EStationStatus Status { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CompanyId { get; set; }

    public string CompanyContactEmail { get; set; } = string.Empty;
}

