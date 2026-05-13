using Modules.Charging.Domain;

namespace Modules.Charging.Infrastructure.Repositories;

internal interface IMaintenanceRepository
{
    IQueryable<Maintenance> Query();
    void Add(Maintenance maintenance);
}
