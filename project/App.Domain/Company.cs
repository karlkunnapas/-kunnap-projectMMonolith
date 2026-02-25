namespace App.Domain;

public class Company : BaseEntity
{
    public LangStr Name { get; set; } = default!;
    public string ContactEmail { get; set; } = default!;
    public string ContactPhone { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<ChargingStation>? ChargingStations { get; set; }
    public ICollection<AppUserCompany>? UserCompanies { get; set; }
    public ICollection<Promotion>? Promotions { get; set; }
}