namespace Shared.Contracts.Tenancy;

public sealed record TenantCompanyContext(
    Guid CompanyId,
    string Slug,
    bool IsActive);

public interface ITenantContext
{
    TenantCompanyContext? Company { get; }

    Guid? CompanyId { get; }

    string? CompanySlug { get; }

    bool IsResolved { get; }

    Guid? CurrentUserId { get; }

    void SetCompany(TenantCompanyContext company);

    void SetCurrentUserId(Guid? userId);
}

public sealed class TenantContext : ITenantContext
{
    public TenantCompanyContext? Company { get; private set; }

    public Guid? CompanyId => Company?.CompanyId;

    public string? CompanySlug => Company?.Slug;

    public bool IsResolved => Company is not null;

    public Guid? CurrentUserId { get; private set; }

    public void SetCompany(TenantCompanyContext company)
    {
        Company = company;
    }

    public void SetCurrentUserId(Guid? userId)
    {
        CurrentUserId = userId;
    }
}
