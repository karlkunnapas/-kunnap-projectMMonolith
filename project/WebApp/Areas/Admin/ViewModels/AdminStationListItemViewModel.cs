namespace WebApp.Areas.Admin.ViewModels;

public class AdminStationListItemViewModel
{
    public Guid StationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
}
