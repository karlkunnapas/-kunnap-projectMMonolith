using Mediator;

namespace Shared.Contracts.Companies.Mediator;

public sealed record IsCompanyActiveQuery(Guid CompanyId) : IQuery<bool>;
