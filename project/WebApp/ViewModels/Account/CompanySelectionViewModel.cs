namespace WebApp.ViewModels.Account;

public class CompanySelectionItemViewModel
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanySlug { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class CompanySelectionViewModel
{
    public List<CompanySelectionItemViewModel> Companies { get; set; } = new();
    public string? ReturnUrl { get; set; }
}