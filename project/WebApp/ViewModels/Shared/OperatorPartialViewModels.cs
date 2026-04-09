namespace WebApp.ViewModels.Shared;

public class KpiCardViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public string AccentClass { get; set; } = "kpi-accent-primary";
}

public class StationHealthBadgeViewModel
{
    public string HealthStatus { get; set; } = string.Empty;
}
