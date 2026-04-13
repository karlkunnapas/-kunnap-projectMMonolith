namespace App.DTO.v1.Station;

/// <summary>
/// Compact station data for station listings.
/// </summary>
public class StationSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public Dictionary<string, string> NameTranslations { get; set; } = new();
    public string Location { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal PricePerKwh { get; set; }
    public decimal MaxPower { get; set; }
    public List<string> ConnectorNames { get; set; } = new();
    public bool? IsCompatibleWithSelectedVehicle { get; set; }
}
