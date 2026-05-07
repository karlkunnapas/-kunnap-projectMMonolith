using System.ComponentModel.DataAnnotations;

namespace WebApp.Areas.Admin.ViewModels;

public class AdminConnectorTypeFormViewModel
{
    public Guid? ConnectorTypeId { get; set; }

    [Required]
    [StringLength(120)]
    public string NameEn { get; set; } = string.Empty;

    [StringLength(120)]
    public string NameEt { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
