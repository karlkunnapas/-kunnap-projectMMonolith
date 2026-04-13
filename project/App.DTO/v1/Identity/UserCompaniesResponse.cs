namespace App.DTO.v1.Identity;

public class UserCompaniesResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public List<UserCompanyItem> Companies { get; set; } = new();
}

public class UserCompanyItem
{
    public Guid MembershipId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string CompanySlug { get; set; } = default!;
    public string Role { get; set; } = default!;
}
