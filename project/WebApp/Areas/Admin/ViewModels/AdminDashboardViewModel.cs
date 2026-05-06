namespace WebApp.Areas.Admin.ViewModels;

public class AdminDashboardViewModel
{
    public int ReservationsInPeriod { get; set; }
    public int TotalCompanies { get; set; }
    public int TotalCompanyUsers { get; set; }
    public int TotalClientUsers { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}
