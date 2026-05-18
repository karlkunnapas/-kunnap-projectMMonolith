using Microsoft.EntityFrameworkCore;
using Modules.Companies.Application.DTO;
using Shared.Contracts;
using System.Data;
using System.Text.Json;

namespace Modules.Companies.Infrastructure;

internal sealed class CompaniesRepository : ICompaniesRepository
{
    private readonly CompaniesDbContext _dbContext;
    private readonly ICompaniesUnitOfWork _unitOfWork;

    public CompaniesRepository(CompaniesDbContext dbContext, ICompaniesUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<CompanyTenantDto?> GetCompanyTenantBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalizedSlug = slug.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            return null;
        }

        return await _dbContext.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.Slug == normalizedSlug)
            .Select(c => new CompanyTenantDto
            {
                CompanyId = c.Id,
                Slug = c.Slug,
                IsActive = c.IsActive
            })
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken ct = default)
    {
        return _dbContext.Companies
            .AsNoTracking()
            .AnyAsync(c => c.Id == companyId, ct);
    }

    public Task<bool> IsCompanyActiveAsync(Guid companyId, CancellationToken ct = default)
    {
        return _dbContext.Companies
            .AsNoTracking()
            .AnyAsync(c => c.Id == companyId && c.IsActive, ct);
    }

    public async Task<bool> HasActiveOwnerMembershipAsync(Guid companyId, Guid userId, CancellationToken ct = default)
    {
        var companyActive = await IsCompanyActiveAsync(companyId, ct);
        if (!companyActive)
        {
            return false;
        }

        return await _dbContext.AppUserCompanies
            .AsNoTracking()
            .AnyAsync(x =>
                x.CompanyId == companyId
                && x.AppUserId == userId
                && x.IsActive
                && x.Role == Domain.ECompanyRole.Owner, ct);
    }

    public async Task<IReadOnlyCollection<AdminCompanyDto>> GetCompaniesForAdminAsync(string? search = null, CancellationToken ct = default)
    {
        var companies = await _dbContext.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(ct);

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            companies = companies.Where(c =>
                    c.Slug.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    c.ContactEmail.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    ParseCompanyName(c.Name).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        var memberCounts = await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(uc => uc.IsActive)
            .GroupBy(uc => uc.CompanyId)
            .Select(group => new
            {
                CompanyId = group.Key,
                Count = group.Select(x => x.AppUserId).Distinct().Count()
            })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count, ct);

        return companies
            .Select(c => new AdminCompanyDto
            {
                CompanyId = c.Id,
                CompanyName = ParseCompanyName(c.Name),
                ContactEmail = c.ContactEmail,
                Slug = c.Slug,
                IsActive = c.IsActive,
                ActiveMembersCount = memberCounts.GetValueOrDefault(c.Id, 0)
            })
            .OrderBy(x => x.CompanyName)
            .ToList();
    }

    public async Task<AdminCompanyDto?> SetCompanyActivationAsync(Guid companyId, bool isActive, CancellationToken ct = default)
    {
        var company = await _dbContext.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);
        if (company == null)
        {
            return null;
        }

        if (company.IsActive != isActive)
        {
            company.IsActive = isActive;
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var activeMembersCount = await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(uc => uc.CompanyId == companyId && uc.IsActive)
            .Select(uc => uc.AppUserId)
            .Distinct()
            .CountAsync(ct);

        return new AdminCompanyDto
        {
            CompanyId = company.Id,
            CompanyName = ParseCompanyName(company.Name),
            ContactEmail = company.ContactEmail,
            Slug = company.Slug,
            IsActive = company.IsActive,
            ActiveMembersCount = activeMembersCount
        };
    }

    public async Task<IReadOnlyCollection<UserCompanyMembershipDto>> GetUserCompaniesAsync(Guid userId, CancellationToken ct = default)
    {
        var items = await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(x => x.AppUserId == userId && x.IsActive)
            .Join(
                _dbContext.Companies.AsNoTracking().Where(c => c.IsActive),
                uc => uc.CompanyId,
                c => c.Id,
                (uc, c) => new UserCompanyMembershipDto
                {
                    MembershipId = uc.Id,
                    CompanyId = c.Id,
                    UserId = uc.AppUserId,
                    CompanyName = ParseCompanyName(c.Name),
                    Slug = c.Slug,
                    Role = uc.Role.ToString(),
                    IsActive = uc.IsActive
                })
            .ToListAsync(ct);

        return items;
    }

    public async Task<UserCompanyMembershipDto?> GetActiveCompanySelectionAsync(Guid userId, Guid companyId, CancellationToken ct = default)
    {
        var item = await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(x => x.AppUserId == userId && x.CompanyId == companyId && x.IsActive)
            .Join(
                _dbContext.Companies.AsNoTracking().Where(c => c.IsActive),
                uc => uc.CompanyId,
                c => c.Id,
                (uc, c) => new UserCompanyMembershipDto
                {
                    MembershipId = uc.Id,
                    CompanyId = c.Id,
                    UserId = uc.AppUserId,
                    CompanyName = ParseCompanyName(c.Name),
                    Slug = c.Slug,
                    Role = uc.Role.ToString(),
                    IsActive = uc.IsActive
                })
            .FirstOrDefaultAsync(ct);

        if (item == null)
        {
            return null;
        }

        return item;
    }

    public async Task<IReadOnlyCollection<CompanyMembershipDto>> GetCompanyMembershipsAsync(Guid companyId, CancellationToken ct = default)
    {
        return await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.JoinedAtUtc)
            .Select(x => new CompanyMembershipDto
            {
                MembershipId = x.Id,
                CompanyId = x.CompanyId,
                UserId = x.AppUserId,
                Role = x.Role.ToString(),
                IsActive = x.IsActive,
                JoinedAtUtc = x.JoinedAtUtc
            })
            .ToListAsync(ct);
    }

    public async Task<CompanyMembershipDto?> GetCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default)
    {
        return await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Id == membershipId)
            .Select(x => new CompanyMembershipDto
            {
                MembershipId = x.Id,
                CompanyId = x.CompanyId,
                UserId = x.AppUserId,
                Role = x.Role.ToString(),
                IsActive = x.IsActive,
                JoinedAtUtc = x.JoinedAtUtc
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CompanyMembershipDto?> GetCompanyMembershipByUserAsync(Guid companyId, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.AppUserId == userId)
            .Select(x => new CompanyMembershipDto
            {
                MembershipId = x.Id,
                CompanyId = x.CompanyId,
                UserId = x.AppUserId,
                Role = x.Role.ToString(),
                IsActive = x.IsActive,
                JoinedAtUtc = x.JoinedAtUtc
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UpsertCompanyMembershipResultDto> UpsertCompanyMembershipAsync(
        UpsertCompanyMembershipDto request,
        CancellationToken ct = default)
    {
        var parsedRole = Enum.TryParse<Domain.ECompanyRole>(request.Role, ignoreCase: true, out var role)
            ? role
            : Domain.ECompanyRole.Employee;

        var existing = await _dbContext.AppUserCompanies
            .FirstOrDefaultAsync(x => x.CompanyId == request.CompanyId && x.AppUserId == request.UserId, ct);

        if (existing != null)
        {
            if (existing.IsActive)
            {
                return new UpsertCompanyMembershipResultDto
                {
                    Membership = ToMembershipDto(existing),
                    Operation = "AlreadyActive"
                };
            }

            existing.IsActive = true;
            existing.Role = parsedRole;
            existing.JoinedAtUtc = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);
            return new UpsertCompanyMembershipResultDto
            {
                Membership = ToMembershipDto(existing),
                Operation = "Reactivated"
            };
        }

        var membership = new Domain.AppUserCompany
        {
            Id = Guid.NewGuid(),
            AppUserId = request.UserId,
            CompanyId = request.CompanyId,
            Role = parsedRole,
            IsActive = true,
            JoinedAtUtc = DateTime.UtcNow
        };

        _dbContext.AppUserCompanies.Add(membership);
        await _unitOfWork.SaveChangesAsync(ct);
        return new UpsertCompanyMembershipResultDto
        {
            Membership = ToMembershipDto(membership),
            Operation = "Created"
        };
    }

    public async Task<CompanyMembershipDto?> UpdateCompanyMembershipRoleAsync(Guid companyId, Guid membershipId, string role, CancellationToken ct = default)
    {
        var membership = await _dbContext.AppUserCompanies
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == membershipId, ct);
        if (membership == null)
        {
            return null;
        }

        membership.Role = Enum.TryParse<Domain.ECompanyRole>(role, ignoreCase: true, out var parsed)
            ? parsed
            : Domain.ECompanyRole.Employee;

        await _unitOfWork.SaveChangesAsync(ct);
        return ToMembershipDto(membership);
    }

    public async Task<CompanyMembershipDto?> DeactivateCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default)
    {
        var membership = await _dbContext.AppUserCompanies
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == membershipId, ct);
        if (membership == null)
        {
            return null;
        }

        membership.IsActive = false;
        await _unitOfWork.SaveChangesAsync(ct);
        return ToMembershipDto(membership);
    }

    public Task<int> CountActiveCompanyOwnersAsync(Guid companyId, CancellationToken ct = default)
    {
        return _dbContext.AppUserCompanies
            .AsNoTracking()
            .CountAsync(x => x.CompanyId == companyId && x.IsActive && x.Role == Domain.ECompanyRole.Owner, ct);
    }

    public Task<bool> HasAnyActiveOwnerMembershipForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return _dbContext.AppUserCompanies
            .AsNoTracking()
            .AnyAsync(x => x.AppUserId == userId && x.IsActive && x.Role == Domain.ECompanyRole.Owner, ct);
    }

    public Task<bool> HasDeactivatedActiveMembershipAsync(Guid userId, CancellationToken ct = default)
    {
        return _dbContext.AppUserCompanies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Join(
                _dbContext.Companies.IgnoreQueryFilters().AsNoTracking(),
                uc => uc.CompanyId,
                c => c.Id,
                (uc, c) => new { uc, c })
            .AnyAsync(
                x => x.uc.AppUserId == userId && x.uc.IsActive && !x.c.IsActive,
                ct);
    }

    public async Task<IReadOnlyCollection<CompanyPromotionDto>> GetCompanyPromotionsAsync(Guid companyId, CancellationToken ct = default)
    {
        var promotions = await _dbContext.Promotions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Code)
            .Select(x => new CompanyPromotionDto
            {
                Id = x.Id,
                Code = x.Code,
                DiscountValue = x.DiscountValue,
                ValidFromUtc = x.ValidFrom,
                ValidToUtc = x.ValidTo,
                IsActive = x.IsActive,
                CompanyId = x.CompanyId
            })
            .ToListAsync(ct);

        await SetCanDeleteAsync(promotions, ct);
        return promotions;
    }

    public async Task<CompanyPromotionDto?> GetCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default)
    {
        var promotion = await _dbContext.Promotions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Id == promotionId)
            .Select(x => new CompanyPromotionDto
            {
                Id = x.Id,
                Code = x.Code,
                DiscountValue = x.DiscountValue,
                ValidFromUtc = x.ValidFrom,
                ValidToUtc = x.ValidTo,
                IsActive = x.IsActive,
                CompanyId = x.CompanyId
            })
            .FirstOrDefaultAsync(ct);

        if (promotion != null)
        {
            promotion.CanDelete = !await HasRelatedEntitiesAsync(promotion.Id, ct);
        }

        return promotion;
    }

    public async Task<PromotionOperationResultDto> CreateCompanyPromotionAsync(
        Guid companyId,
        UpsertCompanyPromotionDto request,
        CancellationToken ct = default)
    {
        var promotion = new Domain.Promotion
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Code = NormalizeCode(request.Code),
            DiscountValue = request.DiscountValue,
            ValidFrom = ToUtc(request.ValidFromUtc),
            ValidTo = ToUtc(request.ValidToUtc),
            IsActive = request.IsActive
        };

        _dbContext.Promotions.Add(promotion);
        await _unitOfWork.SaveChangesAsync(ct);

        return PromotionOperationResultDto.Ok(ToPromotionDto(promotion));
    }

    public async Task<PromotionOperationResultDto> UpdateCompanyPromotionAsync(
        Guid companyId,
        Guid promotionId,
        UpsertCompanyPromotionDto request,
        CancellationToken ct = default)
    {
        var promotion = await _dbContext.Promotions
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == promotionId, ct);
        if (promotion == null)
        {
            return PromotionOperationResultDto.Fail("FORBIDDEN", "Promotion not found or access denied.");
        }

        promotion.Code = NormalizeCode(request.Code);
        promotion.DiscountValue = request.DiscountValue;
        promotion.ValidFrom = ToUtc(request.ValidFromUtc);
        promotion.ValidTo = ToUtc(request.ValidToUtc);
        promotion.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync(ct);
        return PromotionOperationResultDto.Ok(ToPromotionDto(promotion));
    }

    public async Task<bool> DeleteCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default)
    {
        var promotion = await _dbContext.Promotions
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == promotionId, ct);
        if (promotion == null)
        {
            return false;
        }

        if (await HasRelatedEntitiesAsync(promotionId, ct))
        {
            return false;
        }

        _dbContext.Promotions.Remove(promotion);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyCollection<CompanyPromotionDto>> GetSystemPromotionsAsync(CancellationToken ct = default)
    {
        var promotions = await _dbContext.Promotions
            .AsNoTracking()
            .Where(x => x.CompanyId == null)
            .OrderByDescending(x => x.ValidTo)
            .ThenBy(x => x.Code)
            .Select(x => new CompanyPromotionDto
            {
                Id = x.Id,
                Code = x.Code,
                DiscountValue = x.DiscountValue,
                ValidFromUtc = x.ValidFrom,
                ValidToUtc = x.ValidTo,
                IsActive = x.IsActive,
                CompanyId = x.CompanyId
            })
            .ToListAsync(ct);

        await SetCanDeleteAsync(promotions, ct);
        return promotions;
    }

    public async Task<CompanyPromotionDto?> GetSystemPromotionAsync(Guid promotionId, CancellationToken ct = default)
    {
        var promotion = await _dbContext.Promotions
            .AsNoTracking()
            .Where(x => x.CompanyId == null && x.Id == promotionId)
            .Select(x => new CompanyPromotionDto
            {
                Id = x.Id,
                Code = x.Code,
                DiscountValue = x.DiscountValue,
                ValidFromUtc = x.ValidFrom,
                ValidToUtc = x.ValidTo,
                IsActive = x.IsActive,
                CompanyId = x.CompanyId
            })
            .FirstOrDefaultAsync(ct);

        if (promotion != null)
        {
            promotion.CanDelete = !await HasRelatedEntitiesAsync(promotion.Id, ct);
        }

        return promotion;
    }

    public async Task<PromotionOperationResultDto> CreateSystemPromotionAsync(UpsertCompanyPromotionDto request, CancellationToken ct = default)
    {
        var promotion = new Domain.Promotion
        {
            Id = Guid.NewGuid(),
            Code = NormalizeCode(request.Code),
            DiscountValue = request.DiscountValue,
            ValidFrom = ToUtc(request.ValidFromUtc),
            ValidTo = ToUtc(request.ValidToUtc),
            IsActive = request.IsActive,
            CompanyId = null
        };

        _dbContext.Promotions.Add(promotion);
        await _unitOfWork.SaveChangesAsync(ct);
        return PromotionOperationResultDto.Ok(ToPromotionDto(promotion));
    }

    public async Task<PromotionOperationResultDto> UpdateSystemPromotionAsync(Guid promotionId, UpsertCompanyPromotionDto request, CancellationToken ct = default)
    {
        var promotion = await _dbContext.Promotions
            .FirstOrDefaultAsync(x => x.CompanyId == null && x.Id == promotionId, ct);
        if (promotion == null)
        {
            return PromotionOperationResultDto.Fail("NOT_FOUND", "System promotion not found.");
        }

        promotion.Code = NormalizeCode(request.Code);
        promotion.DiscountValue = request.DiscountValue;
        promotion.ValidFrom = ToUtc(request.ValidFromUtc);
        promotion.ValidTo = ToUtc(request.ValidToUtc);
        promotion.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync(ct);
        return PromotionOperationResultDto.Ok(ToPromotionDto(promotion));
    }

    public async Task<bool> DeleteSystemPromotionAsync(Guid promotionId, CancellationToken ct = default)
    {
        var promotion = await _dbContext.Promotions
            .FirstOrDefaultAsync(x => x.CompanyId == null && x.Id == promotionId, ct);
        if (promotion == null)
        {
            return false;
        }

        if (await HasRelatedEntitiesAsync(promotionId, ct))
        {
            return false;
        }

        _dbContext.Promotions.Remove(promotion);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyCollection<UserPromotionDto>> GetUserPromotionsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.UserPromotions
            .AsNoTracking()
            .Include(x => x.Promotion)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.AddedAt)
            .Select(x => new UserPromotionDto
            {
                Id = x.Id,
                UserId = x.UserId,
                PromotionId = x.PromotionId,
                AddedAtUtc = x.AddedAt,
                IsUsed = x.IsUsed,
                Promotion = x.Promotion == null
                    ? null
                    : new CompanyPromotionDto
                    {
                        Id = x.Promotion.Id,
                        Code = x.Promotion.Code,
                        DiscountValue = x.Promotion.DiscountValue,
                        ValidFromUtc = x.Promotion.ValidFrom,
                        ValidToUtc = x.Promotion.ValidTo,
                        IsActive = x.Promotion.IsActive,
                        CompanyId = x.Promotion.CompanyId
                    }
            })
            .ToListAsync(ct);
    }

    public async Task<PromotionOperationResultDto> RedeemPromotionAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var normalizedCode = NormalizeCode(code);
        var nowUtc = DateTime.UtcNow;

        var candidates = await _dbContext.Promotions
            .Where(x => x.IsActive
                        && x.Code == normalizedCode
                        && x.ValidFrom <= nowUtc
                        && x.ValidTo >= nowUtc)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return PromotionOperationResultDto.Fail("NOT_FOUND", "Promotion code is invalid or expired.");
        }

        if (candidates.Count > 1)
        {
            return PromotionOperationResultDto.Fail("VALIDATION", "Promotion code is ambiguous. Contact support.");
        }

        var promotion = candidates[0];
        var existing = await _dbContext.UserPromotions
            .AnyAsync(x => x.UserId == userId && x.PromotionId == promotion.Id, ct);
        if (existing)
        {
            return PromotionOperationResultDto.Fail("VALIDATION", "Promotion has already been redeemed by this user.");
        }

        _dbContext.UserPromotions.Add(new Domain.UserPromotion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PromotionId = promotion.Id,
            AddedAt = nowUtc,
            IsUsed = false
        });

        await _unitOfWork.SaveChangesAsync(ct);
        return PromotionOperationResultDto.Ok(ToPromotionDto(promotion));
    }

    public async Task<bool> RemoveUserPromotionAsync(Guid userId, Guid userPromotionId, CancellationToken ct = default)
    {
        var userPromotion = await _dbContext.UserPromotions
            .FirstOrDefaultAsync(x => x.Id == userPromotionId && x.UserId == userId, ct);
        if (userPromotion == null)
        {
            return false;
        }

        if (userPromotion.IsUsed)
        {
            return false;
        }

        userPromotion.IsUsed = true;
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<UserPromotionDto?> GetValidUserPromotionByCodeAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var normalizedCode = NormalizeCode(code);
        var nowUtc = DateTime.UtcNow;

        return await _dbContext.UserPromotions
            .AsNoTracking()
            .Include(x => x.Promotion)
            .Where(x => x.UserId == userId
                        && !x.IsUsed
                        && x.Promotion != null
                        && x.Promotion.IsActive
                        && x.Promotion.Code == normalizedCode
                        && x.Promotion.ValidFrom <= nowUtc
                        && x.Promotion.ValidTo >= nowUtc)
            .OrderByDescending(x => x.AddedAt)
            .Select(x => new UserPromotionDto
            {
                Id = x.Id,
                UserId = x.UserId,
                PromotionId = x.PromotionId,
                AddedAtUtc = x.AddedAt,
                IsUsed = x.IsUsed,
                Promotion = x.Promotion == null
                    ? null
                    : new CompanyPromotionDto
                    {
                        Id = x.Promotion.Id,
                        Code = x.Promotion.Code,
                        DiscountValue = x.Promotion.DiscountValue,
                        ValidFromUtc = x.Promotion.ValidFrom,
                        ValidToUtc = x.Promotion.ValidTo,
                        IsActive = x.Promotion.IsActive,
                        CompanyId = x.Promotion.CompanyId
                    }
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyCollection<CompanyAuditEntryDto>> GetCompanyAuditAsync(
        Guid companyId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.AtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.AtUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var normalizedEntityName = entityName.Trim();
            query = query.Where(x => x.EntityName == normalizedEntityName);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim();
            query = query.Where(x => x.Action == normalizedAction);
        }

        return await query
            .OrderByDescending(x => x.AtUtc)
            .Select(x => new CompanyAuditEntryDto
            {
                Id = x.Id,
                CompanyId = x.CompanyId,
                UserName = x.UserName,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                Action = x.Action,
                AtUtc = x.AtUtc,
                ChangesJson = x.ChangesJson
            })
            .ToListAsync(ct);
    }

    public async Task<CompanyAuditTrailDto> GetAuditTrailAsync(
        string entityName,
        Guid entityId,
        Guid? companyId = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.EntityName == entityName && x.EntityId == entityId);

        if (companyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == companyId.Value);
        }

        var entries = await query
            .OrderBy(x => x.AtUtc)
            .Select(x => new CompanyAuditEntryDto
            {
                Id = x.Id,
                CompanyId = x.CompanyId,
                UserName = x.UserName,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                Action = x.Action,
                AtUtc = x.AtUtc,
                ChangesJson = x.ChangesJson
            })
            .ToListAsync(ct);

        return new CompanyAuditTrailDto
        {
            EntityName = entityName,
            EntityId = entityId,
            Entries = entries
        };
    }

    public async Task<CreateCompanyWithOwnerMembershipResultDto> CreateCompanyWithOwnerMembershipAsync(
        CreateCompanyWithOwnerMembershipDto request,
        CancellationToken ct = default)
    {
        var normalizedSlug = request.CompanySlug.Trim().ToLowerInvariant();
        var slugExists = await _dbContext.Companies
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Slug == normalizedSlug, ct);
        if (slugExists)
        {
            return CreateCompanyWithOwnerMembershipResultDto.Fail("DUPLICATE_SLUG", "A company with this slug already exists.");
        }

        var companyId = Guid.NewGuid();
        var company = new Domain.Company
        {
            Id = companyId,
            Name = new LangStr
            {
                ["en"] = request.CompanyName.Trim()
            },
            ContactEmail = request.ContactEmail.Trim(),
            ContactPhone = request.ContactPhone.Trim(),
            Slug = normalizedSlug,
            IsActive = true
        };
        var membership = new Domain.AppUserCompany
        {
            Id = Guid.NewGuid(),
            AppUserId = request.OwnerUserId,
            CompanyId = companyId,
            Role = Domain.ECompanyRole.Owner,
            IsActive = true,
            JoinedAtUtc = DateTime.UtcNow
        };

        _dbContext.Companies.Add(company);
        _dbContext.AppUserCompanies.Add(membership);
        await _unitOfWork.SaveChangesAsync(ct);

        return CreateCompanyWithOwnerMembershipResultDto.Ok(companyId);
    }

    private static string ParseCompanyName(LangStr? raw)
    {
        return raw?.Translate() ?? string.Empty;
    }

    private static CompanyPromotionDto ToPromotionDto(Domain.Promotion x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        DiscountValue = x.DiscountValue,
        ValidFromUtc = x.ValidFrom,
        ValidToUtc = x.ValidTo,
        IsActive = x.IsActive,
        CompanyId = x.CompanyId
    };

    private async Task SetCanDeleteAsync(List<CompanyPromotionDto> promotions, CancellationToken ct)
    {
        if (promotions.Count == 0)
        {
            return;
        }

        var inUseIds = await GetInUsePromotionIdsAsync(promotions.Select(p => p.Id).ToArray(), ct);
        foreach (var promotion in promotions)
        {
            promotion.CanDelete = !inUseIds.Contains(promotion.Id);
        }
    }

    private async Task<bool> HasRelatedEntitiesAsync(Guid promotionId, CancellationToken ct)
    {
        var inUseIds = await GetInUsePromotionIdsAsync(new[] { promotionId }, ct);
        return inUseIds.Contains(promotionId);
    }

    private async Task<HashSet<Guid>> GetInUsePromotionIdsAsync(Guid[] promotionIds, CancellationToken ct)
    {
        var ids = promotionIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new HashSet<Guid>();
        }

        var inUseIds = await _dbContext.UserPromotions
            .AsNoTracking()
            .Where(up => ids.Contains(up.PromotionId))
            .Select(up => up.PromotionId)
            .Distinct()
            .ToHashSetAsync(ct);

        await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT DISTINCT "PromotionId"
            FROM "Reservations"
            WHERE "PromotionId" IS NOT NULL AND "PromotionId" = ANY(@promotionIds)
            UNION
            SELECT DISTINCT "PromotionId"
            FROM "ChargingSessions"
            WHERE "PromotionId" IS NOT NULL AND "PromotionId" = ANY(@promotionIds)
            """;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "promotionIds";
        parameter.Value = ids;
        command.Parameters.Add(parameter);

        var connection = command.Connection!;
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(0))
                {
                    inUseIds.Add(reader.GetGuid(0));
                }
            }
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }

        return inUseIds;
    }

    private static string NormalizeCode(string code)
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
        };
    }

    private static CompanyAuditEntryDto ToAuditEntryDto(Domain.AuditLog x) => new()
    {
        Id = x.Id,
        CompanyId = x.CompanyId,
        UserName = x.UserName,
        EntityName = x.EntityName,
        EntityId = x.EntityId,
        Action = x.Action,
        AtUtc = x.AtUtc,
        ChangesJson = x.ChangesJson
    };

    private static CompanyMembershipDto ToMembershipDto(Domain.AppUserCompany x) => new()
    {
        MembershipId = x.Id,
        CompanyId = x.CompanyId,
        UserId = x.AppUserId,
        Role = x.Role.ToString(),
        IsActive = x.IsActive,
        JoinedAtUtc = x.JoinedAtUtc
    };
}
