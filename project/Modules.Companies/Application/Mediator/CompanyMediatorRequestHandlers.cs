using Mediator;
using Modules.Companies.Application.Services;
using Shared.Contracts.Companies.Mediator;

namespace Modules.Companies.Application.Mediator;

internal sealed class IsCompanyActiveQueryHandler : IQueryHandler<IsCompanyActiveQuery, bool>
{
    private readonly ICompaniesApplicationService _companies;

    public IsCompanyActiveQueryHandler(ICompaniesApplicationService companies)
    {
        _companies = companies;
    }

    public ValueTask<bool> Handle(IsCompanyActiveQuery query, CancellationToken cancellationToken)
        => new(_companies.IsCompanyActiveAsync(query.CompanyId, cancellationToken));
}
