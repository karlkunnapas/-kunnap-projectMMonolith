namespace Modules.Companies.Domain;

internal sealed class AppUserCompany
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public Guid CompanyId { get; set; }
    public ECompanyRole Role { get; set; } = ECompanyRole.Employee;
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAtUtc { get; set; }

    public Company? Company { get; set; }
}
