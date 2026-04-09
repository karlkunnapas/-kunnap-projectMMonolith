using App.Domain;

namespace App.DAL.EF.Repositories.Interfaces;

public interface IUserPromotionRepository
{
    Task<List<UserPromotion>> GetByUserIdAsync(Guid userId);
    Task<UserPromotion?> GetByIdForUserAsync(Guid id, Guid userId);
    Task<UserPromotion?> GetByUserAndPromotionAsync(Guid userId, Guid promotionId);
    Task<UserPromotion?> GetValidByCodeForUserAsync(Guid userId, string code, DateTime atUtc);
    Task AddAsync(UserPromotion userPromotion);
    void Remove(UserPromotion userPromotion);
}
