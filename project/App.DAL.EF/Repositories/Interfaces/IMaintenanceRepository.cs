using App.Domain;

namespace App.DAL.EF.Repositories.Interfaces;

public interface IMaintenanceRepository
{
    Task<Maintenance?> GetByIdAsync(Guid id);
    Task<Maintenance?> GetByIdForCompanyAsync(Guid id, Guid companyId);
    Task<List<Maintenance>> GetByCompanyAsync(Guid companyId, bool includeResolved = true);
    Task<List<Maintenance>> GetOpenIssuesByCompanyAsync(Guid companyId);
    Task<List<Maintenance>> GetByStationIdAsync(Guid stationId);
    Task<int> GetUnresolvedCountByStationAsync(Guid stationId);
    Task<bool> HasUnresolvedIssuesAsync(Guid stationId);
    Task AddAsync(Maintenance issue);
    void Update(Maintenance issue);
}
