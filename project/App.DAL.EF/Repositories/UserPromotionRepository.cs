using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories;

public class UserPromotionRepository : IUserPromotionRepository
{
    private readonly AppDbContext _context;

    public UserPromotionRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<UserPromotion>> GetByUserIdAsync(Guid userId)
    {
        var nowUtc = DateTime.UtcNow;

        return _context.UserPromotions
            .Include(up => up.Promotion)
            .Where(up => up.UserId == userId
                         && !up.IsUsed
                         && up.Promotion != null
                         && up.Promotion.IsActive
                         && up.Promotion.ValidFrom <= nowUtc
                         && up.Promotion.ValidTo >= nowUtc)
            .OrderByDescending(up => up.AddedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<UserPromotion?> GetByIdForUserAsync(Guid id, Guid userId)
    {
        return _context.UserPromotions
            .AsTracking()
            .FirstOrDefaultAsync(up => up.Id == id && up.UserId == userId);
    }

    public Task<UserPromotion?> GetByUserAndPromotionAsync(Guid userId, Guid promotionId)
    {
        return _context.UserPromotions
            .AsTracking()
            .FirstOrDefaultAsync(up => up.UserId == userId && up.PromotionId == promotionId);
    }

    public Task<UserPromotion?> GetValidByCodeForUserAsync(Guid userId, string code, DateTime atUtc)
    {
        return _context.UserPromotions
            .Include(up => up.Promotion)
            .FirstOrDefaultAsync(up => up.UserId == userId
                                       && !up.IsUsed
                                       && up.Promotion != null
                                       && up.Promotion.Code.ToLower() == code.ToLower()
                                       && up.Promotion.IsActive
                                       && up.Promotion.ValidFrom <= atUtc
                                       && up.Promotion.ValidTo >= atUtc);
    }

    public Task AddAsync(UserPromotion userPromotion)
    {
        return _context.UserPromotions.AddAsync(userPromotion).AsTask();
    }

    public void Remove(UserPromotion userPromotion)
    {
        _context.UserPromotions.Remove(userPromotion);
    }
}
