namespace Modules.Companies.Application.DTO;

internal sealed class CompanyTenantDto
{
    public Guid CompanyId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
