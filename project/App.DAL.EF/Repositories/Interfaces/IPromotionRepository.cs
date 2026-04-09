using App.Domain;

namespace App.DAL.EF.Repositories.Interfaces;

public interface IPromotionRepository
{
    Task<List<Promotion>> GetByCompanyAsync(Guid companyId);
    Task<Promotion?> GetByIdForCompanyAsync(Guid id, Guid companyId);
    Task<List<Promotion>> GetActiveByCodeAsync(string code, DateTime atUtc);
    Task AddAsync(Promotion promotion);
    void Update(Promotion promotion);
    void Remove(Promotion promotion);
}
