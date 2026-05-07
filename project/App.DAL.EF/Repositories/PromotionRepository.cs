using App.DAL.EF.Repositories.Interfaces;
using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.EF.Repositories;

public class PromotionRepository : IPromotionRepository
{
    private readonly AppDbContext _context;

    public PromotionRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Promotion?> GetByIdAsync(Guid id)
    {
        return _context.Promotions
            .Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public Task<List<Promotion>> GetByCompanyAsync(Guid companyId)
    {
        return _context.Promotions
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.ValidTo)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<List<Promotion>> GetAllWithCompanyAsync()
    {
        return _context.Promotions
            .Include(p => p.Company)
            .OrderByDescending(p => p.ValidTo)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<Promotion?> GetByIdForCompanyAsync(Guid id, Guid companyId)
    {
        return _context.Promotions
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId);
    }

    public Task<List<Promotion>> GetActiveByCodeAsync(string code, DateTime atUtc)
    {
        return _context.Promotions
            .Where(p => p.Code.ToLower() == code.ToLower()
                        && p.IsActive
                        && p.ValidFrom <= atUtc
                        && p.ValidTo >= atUtc)
            .OrderByDescending(p => p.ValidFrom)
            .ToListAsync();
    }

    public Task AddAsync(Promotion promotion)
    {
        return _context.Promotions.AddAsync(promotion).AsTask();
    }

    public void Update(Promotion promotion)
    {
        _context.Promotions.Update(promotion);
    }

    public void Remove(Promotion promotion)
    {
        _context.Promotions.Remove(promotion);
    }
}
