using Microsoft.EntityFrameworkCore;
using Modules.Companies.Infrastructure;
using Shared.Contracts.Companies;
using System.Text.Json;

namespace Modules.Companies.Application;

internal sealed class CompaniesModuleApi : ICompaniesModuleApi
{
    private readonly CompaniesDbContext _dbContext;

    public CompaniesModuleApi(CompaniesDbContext dbContext)
    {
        _dbContext = dbContext;
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

    public async Task<IReadOnlyCollection<AdminCompanyContract>> GetCompaniesForAdminAsync(string? search = null, CancellationToken ct = default)
    {
        var query = _dbContext.Companies
            .IgnoreQueryFilters()
            .AsNoTracking();

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(c =>
                c.Slug.Contains(normalizedSearch) ||
                c.ContactEmail.Contains(normalizedSearch) ||
                c.Name.Contains(normalizedSearch));
        }

        var companies = await query.ToListAsync(ct);
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
            .Select(c => new AdminCompanyContract
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

    public async Task<AdminCompanyContract?> SetCompanyActivationAsync(Guid companyId, bool isActive, CancellationToken ct = default)
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
            await _dbContext.SaveChangesAsync(ct);
        }

        var activeMembersCount = await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(uc => uc.CompanyId == companyId && uc.IsActive)
            .Select(uc => uc.AppUserId)
            .Distinct()
            .CountAsync(ct);

        return new AdminCompanyContract
        {
            CompanyId = company.Id,
            CompanyName = ParseCompanyName(company.Name),
            ContactEmail = company.ContactEmail,
            Slug = company.Slug,
            IsActive = company.IsActive,
            ActiveMembersCount = activeMembersCount
        };
    }

    public async Task<IReadOnlyCollection<UserCompanyMembershipContract>> GetUserCompaniesAsync(Guid userId, CancellationToken ct = default)
    {
        var items = await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(x => x.AppUserId == userId && x.IsActive)
            .Join(
                _dbContext.Companies.AsNoTracking().Where(c => c.IsActive),
                uc => uc.CompanyId,
                c => c.Id,
                (uc, c) => new UserCompanyMembershipContract
                {
                    MembershipId = uc.Id,
                    CompanyId = c.Id,
                    UserId = uc.AppUserId,
                    CompanyName = c.Name,
                    Slug = c.Slug,
                    Role = uc.Role.ToString(),
                    IsActive = uc.IsActive
                })
            .ToListAsync(ct);

        foreach (var item in items)
        {
            item.CompanyName = ParseCompanyName(item.CompanyName);
        }

        return items;
    }

    public async Task<UserCompanyMembershipContract?> GetActiveCompanySelectionAsync(Guid userId, Guid companyId, CancellationToken ct = default)
    {
        var item = await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(x => x.AppUserId == userId && x.CompanyId == companyId && x.IsActive)
            .Join(
                _dbContext.Companies.AsNoTracking().Where(c => c.IsActive),
                uc => uc.CompanyId,
                c => c.Id,
                (uc, c) => new UserCompanyMembershipContract
                {
                    MembershipId = uc.Id,
                    CompanyId = c.Id,
                    UserId = uc.AppUserId,
                    CompanyName = c.Name,
                    Slug = c.Slug,
                    Role = uc.Role.ToString(),
                    IsActive = uc.IsActive
                })
            .FirstOrDefaultAsync(ct);

        if (item == null)
        {
            return null;
        }

        item.CompanyName = ParseCompanyName(item.CompanyName);
        return item;
    }

    public async Task<IReadOnlyCollection<CompanyMembershipContract>> GetCompanyMembershipsAsync(Guid companyId, CancellationToken ct = default)
    {
        return await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.JoinedAtUtc)
            .Select(x => new CompanyMembershipContract
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

    public async Task<CompanyMembershipContract?> GetCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default)
    {
        return await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Id == membershipId)
            .Select(x => new CompanyMembershipContract
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

    public async Task<CompanyMembershipContract?> GetCompanyMembershipByUserAsync(Guid companyId, Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.AppUserCompanies
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.AppUserId == userId)
            .Select(x => new CompanyMembershipContract
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

    public async Task<UpsertCompanyMembershipResultContract> UpsertCompanyMembershipAsync(
        UpsertCompanyMembershipContract request,
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
                return new UpsertCompanyMembershipResultContract
                {
                    Membership = ToMembershipContract(existing),
                    Operation = "AlreadyActive"
                };
            }

            existing.IsActive = true;
            existing.Role = parsedRole;
            existing.JoinedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return new UpsertCompanyMembershipResultContract
            {
                Membership = ToMembershipContract(existing),
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
        await _dbContext.SaveChangesAsync(ct);
        return new UpsertCompanyMembershipResultContract
        {
            Membership = ToMembershipContract(membership),
            Operation = "Created"
        };
    }

    public async Task<CompanyMembershipContract?> UpdateCompanyMembershipRoleAsync(Guid companyId, Guid membershipId, string role, CancellationToken ct = default)
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

        await _dbContext.SaveChangesAsync(ct);
        return ToMembershipContract(membership);
    }

    public async Task<CompanyMembershipContract?> DeactivateCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default)
    {
        var membership = await _dbContext.AppUserCompanies
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == membershipId, ct);
        if (membership == null)
        {
            return null;
        }

        membership.IsActive = false;
        await _dbContext.SaveChangesAsync(ct);
        return ToMembershipContract(membership);
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

    public async Task<IReadOnlyCollection<CompanyPromotionContract>> GetCompanyPromotionsAsync(Guid companyId, CancellationToken ct = default)
    {
        return await _dbContext.Promotions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Code)
            .Select(x => new CompanyPromotionContract
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
    }

    public async Task<CompanyPromotionContract?> GetCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default)
    {
        return await _dbContext.Promotions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Id == promotionId)
            .Select(x => new CompanyPromotionContract
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
    }

    public async Task<PromotionOperationResultContract> CreateCompanyPromotionAsync(
        Guid companyId,
        UpsertCompanyPromotionContract request,
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
        await _dbContext.SaveChangesAsync(ct);

        return PromotionOperationResultContract.Ok(ToPromotionContract(promotion));
    }

    public async Task<PromotionOperationResultContract> UpdateCompanyPromotionAsync(
        Guid companyId,
        Guid promotionId,
        UpsertCompanyPromotionContract request,
        CancellationToken ct = default)
    {
        var promotion = await _dbContext.Promotions
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == promotionId, ct);
        if (promotion == null)
        {
            return PromotionOperationResultContract.Fail("FORBIDDEN", "Promotion not found or access denied.");
        }

        promotion.Code = NormalizeCode(request.Code);
        promotion.DiscountValue = request.DiscountValue;
        promotion.ValidFrom = ToUtc(request.ValidFromUtc);
        promotion.ValidTo = ToUtc(request.ValidToUtc);
        promotion.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(ct);
        return PromotionOperationResultContract.Ok(ToPromotionContract(promotion));
    }

    public async Task<bool> DeleteCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default)
    {
        var promotion = await _dbContext.Promotions
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == promotionId, ct);
        if (promotion == null)
        {
            return false;
        }

        _dbContext.Promotions.Remove(promotion);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyCollection<UserPromotionContract>> GetUserPromotionsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.UserPromotions
            .AsNoTracking()
            .Include(x => x.Promotion)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.AddedAt)
            .Select(x => new UserPromotionContract
            {
                Id = x.Id,
                UserId = x.UserId,
                PromotionId = x.PromotionId,
                AddedAtUtc = x.AddedAt,
                IsUsed = x.IsUsed,
                Promotion = x.Promotion == null
                    ? null
                    : new CompanyPromotionContract
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

    public async Task<PromotionOperationResultContract> RedeemPromotionAsync(Guid userId, string code, CancellationToken ct = default)
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
            return PromotionOperationResultContract.Fail("NOT_FOUND", "Promotion code is invalid or expired.");
        }

        if (candidates.Count > 1)
        {
            return PromotionOperationResultContract.Fail("VALIDATION", "Promotion code is ambiguous. Contact support.");
        }

        var promotion = candidates[0];
        var existing = await _dbContext.UserPromotions
            .AnyAsync(x => x.UserId == userId && x.PromotionId == promotion.Id, ct);
        if (existing)
        {
            return PromotionOperationResultContract.Fail("VALIDATION", "Promotion is already in your wallet.");
        }

        _dbContext.UserPromotions.Add(new Domain.UserPromotion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PromotionId = promotion.Id,
            AddedAt = nowUtc,
            IsUsed = false
        });

        await _dbContext.SaveChangesAsync(ct);
        return PromotionOperationResultContract.Ok(ToPromotionContract(promotion));
    }

    public async Task<bool> RemoveUserPromotionAsync(Guid userId, Guid userPromotionId, CancellationToken ct = default)
    {
        var userPromotion = await _dbContext.UserPromotions
            .FirstOrDefaultAsync(x => x.Id == userPromotionId && x.UserId == userId, ct);
        if (userPromotion == null)
        {
            return false;
        }

        _dbContext.UserPromotions.Remove(userPromotion);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<UserPromotionContract?> GetValidUserPromotionByCodeAsync(Guid userId, string code, CancellationToken ct = default)
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
            .Select(x => new UserPromotionContract
            {
                Id = x.Id,
                UserId = x.UserId,
                PromotionId = x.PromotionId,
                AddedAtUtc = x.AddedAt,
                IsUsed = x.IsUsed,
                Promotion = x.Promotion == null
                    ? null
                    : new CompanyPromotionContract
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

    public async Task<IReadOnlyCollection<CompanyAuditEntryContract>> GetCompanyAuditAsync(
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
            .Select(x => new CompanyAuditEntryContract
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

    public async Task<CompanyAuditTrailContract> GetAuditTrailAsync(
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
            .Select(x => new CompanyAuditEntryContract
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

        return new CompanyAuditTrailContract
        {
            EntityName = entityName,
            EntityId = entityId,
            Entries = entries
        };
    }

    public async Task<CreateCompanyWithOwnerMembershipResultContract> CreateCompanyWithOwnerMembershipAsync(
        CreateCompanyWithOwnerMembershipContract request,
        CancellationToken ct = default)
    {
        var normalizedSlug = request.CompanySlug.Trim().ToLowerInvariant();
        var slugExists = await _dbContext.Companies
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Slug == normalizedSlug, ct);
        if (slugExists)
        {
            return CreateCompanyWithOwnerMembershipResultContract.Fail("DUPLICATE_SLUG", "A company with this slug already exists.");
        }

        var companyId = Guid.NewGuid();
        var company = new Domain.Company
        {
            Id = companyId,
            Name = JsonSerializer.Serialize(new Dictionary<string, string> { ["en"] = request.CompanyName.Trim() }),
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
        await _dbContext.SaveChangesAsync(ct);

        return CreateCompanyWithOwnerMembershipResultContract.Ok(companyId);
    }

    private static string ParseCompanyName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (!raw.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return raw;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return raw;
            }

            if (root.TryGetProperty("en", out var en) && en.ValueKind == JsonValueKind.String)
            {
                return en.GetString() ?? raw;
            }

            var firstString = root.EnumerateObject()
                .FirstOrDefault(x => x.Value.ValueKind == JsonValueKind.String)
                .Value;

            return firstString.ValueKind == JsonValueKind.String
                ? firstString.GetString() ?? raw
                : raw;
        }
        catch
        {
            return raw;
        }
    }

    private static CompanyPromotionContract ToPromotionContract(Domain.Promotion x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        DiscountValue = x.DiscountValue,
        ValidFromUtc = x.ValidFrom,
        ValidToUtc = x.ValidTo,
        IsActive = x.IsActive,
        CompanyId = x.CompanyId
    };

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

    private static CompanyAuditEntryContract ToAuditEntryContract(Domain.AuditLog x) => new()
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

    private static CompanyMembershipContract ToMembershipContract(Domain.AppUserCompany x) => new()
    {
        MembershipId = x.Id,
        CompanyId = x.CompanyId,
        UserId = x.AppUserId,
        Role = x.Role.ToString(),
        IsActive = x.IsActive,
        JoinedAtUtc = x.JoinedAtUtc
    };
}
