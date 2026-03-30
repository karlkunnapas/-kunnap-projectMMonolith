using App.Domain;

namespace App.DAL.EF;

public interface ITenantContext
{
    Company? Company { get; }

    Guid? CompanyId { get; }

    string? CompanySlug { get; }

    bool IsResolved { get; }

    Guid? CurrentUserId { get; }

    void SetCompany(Company company);

    void SetCurrentUserId(Guid? userId);
}

public sealed class TenantContext : ITenantContext
{
    public Company? Company { get; private set; }

    public Guid? CompanyId => Company?.Id;

    public string? CompanySlug => Company?.Slug;

    public bool IsResolved => Company is not null;

    public Guid? CurrentUserId { get; private set; }

    public void SetCompany(Company company)
    {
        Company = company;
    }

    public void SetCurrentUserId(Guid? userId)
    {
        CurrentUserId = userId;
    }
}