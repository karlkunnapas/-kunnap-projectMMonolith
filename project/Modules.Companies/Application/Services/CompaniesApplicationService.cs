using Modules.Companies.Application.Mappers;
using Modules.Companies.Infrastructure;
using Mediator;
using Shared.Contracts.Companies;
using Shared.Contracts.Companies.Events;
using Shared.Contracts.Users;
using Shared.Contracts.Users.Mediator;

namespace Modules.Companies.Application.Services;

internal sealed class CompaniesApplicationService : ICompaniesApplicationService
{
    private enum CompanyMembershipRole
    {
        Customer = 0,
        Employee = 1,
        Manager = 2,
        Owner = 3
    }

    private readonly ICompaniesRepository _companiesRepository;
    private readonly IMediator _mediator;

    public CompaniesApplicationService(ICompaniesRepository companiesRepository, IMediator mediator)
    {
        _companiesRepository = companiesRepository;
        _mediator = mediator;
    }

    public async Task<CompanyTenantContract?> GetCompanyTenantBySlugAsync(
        string slug,
        CancellationToken ct = default)
    {
        var dto = await _companiesRepository.GetCompanyTenantBySlugAsync(slug, ct);
        return dto is { } ? CompaniesContractMapper.ToContract(dto) : null;
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken ct = default)
    {
        return _companiesRepository.CompanyExistsAsync(companyId, ct);
    }

    public Task<bool> IsCompanyActiveAsync(Guid companyId, CancellationToken ct = default)
    {
        return _companiesRepository.IsCompanyActiveAsync(companyId, ct);
    }

    public Task<bool> HasActiveOwnerMembershipAsync(
        Guid companyId,
        Guid userId,
        CancellationToken ct = default)
    {
        return _companiesRepository.HasActiveOwnerMembershipAsync(companyId, userId, ct);
    }

    public async Task<IReadOnlyCollection<AdminCompanyContract>> GetCompaniesForAdminAsync(
        string? search = null,
        CancellationToken ct = default)
    {
        var companies = await _companiesRepository.GetCompaniesForAdminAsync(search, ct);
        return companies
            .Select(CompaniesContractMapper.ToContract)
            .ToList();
    }

    public async Task<AdminCompanyContract?> SetCompanyActivationAsync(
        Guid companyId,
        bool isActive,
        CancellationToken ct = default)
    {
        var dto = await _companiesRepository.SetCompanyActivationAsync(companyId, isActive, ct);
        return dto is { } ? CompaniesContractMapper.ToContract(dto) : null;
    }

    public async Task<IReadOnlyCollection<UserCompanyMembershipContract>> GetUserCompaniesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var companies = await _companiesRepository.GetUserCompaniesAsync(userId, ct);
        return companies
            .Select(CompaniesContractMapper.ToContract)
            .ToList();
    }

    public async Task<UserCompanyMembershipContract?> GetActiveCompanySelectionAsync(
        Guid userId,
        Guid companyId,
        CancellationToken ct = default)
    {
        var dto = await _companiesRepository.GetActiveCompanySelectionAsync(userId, companyId, ct);
        return dto is { } ? CompaniesContractMapper.ToContract(dto) : null;
    }

    public async Task<UserCompanyMembershipContract?> SelectActiveCompanyAsync(
        Guid userId,
        Guid companyId,
        string actorUserName,
        CancellationToken ct = default)
    {
        var membership = await GetActiveCompanySelectionAsync(userId, companyId, ct);
        if (membership == null)
        {
            return null;
        }

        await LogAuditMutationAsync(
            membership.CompanyId,
            actorUserName,
            "AppUserCompany",
            membership.MembershipId,
            "CompanySwitched",
            $"{{\"userId\":\"{userId}\",\"role\":\"{membership.Role}\"}}",
            ct);

        return membership;
    }

    public async Task<bool> HasCompanyRoleAsync(
        Guid companyId,
        Guid userId,
        string minimumRole,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<CompanyMembershipRole>(minimumRole, true, out var minRole))
        {
            return false;
        }

        var membership = await _companiesRepository.GetCompanyMembershipByUserAsync(companyId, userId, ct);
        if (membership == null || !membership.IsActive)
        {
            return false;
        }

        return Enum.TryParse<CompanyMembershipRole>(membership.Role, true, out var actualRole) && actualRole >= minRole;
    }

    public async Task<IReadOnlyCollection<CompanyMembershipContract>> GetCompanyMembershipsAsync(
        Guid companyId,
        CancellationToken ct = default)
    {
        var memberships = await _companiesRepository.GetCompanyMembershipsAsync(companyId, ct);
        return memberships
            .Select(CompaniesContractMapper.ToContract)
            .ToList();
    }

    public async Task<CompanyMembershipContract?> GetCompanyMembershipAsync(
        Guid companyId,
        Guid membershipId,
        CancellationToken ct = default)
    {
        var dto = await _companiesRepository.GetCompanyMembershipAsync(companyId, membershipId, ct);
        return dto is { } ? CompaniesContractMapper.ToContract(dto) : null;
    }

    public async Task<CompanyMembershipContract?> GetCompanyMembershipByUserAsync(
        Guid companyId,
        Guid userId,
        CancellationToken ct = default)
    {
        var dto = await _companiesRepository.GetCompanyMembershipByUserAsync(companyId, userId, ct);
        return dto is { } ? CompaniesContractMapper.ToContract(dto) : null;
    }

    public async Task<UpsertCompanyMembershipResultContract> UpsertCompanyMembershipAsync(
        UpsertCompanyMembershipContract request,
        CancellationToken ct = default)
    {
        var dto = CompaniesContractMapper.ToDto(request);
        var result = await _companiesRepository.UpsertCompanyMembershipAsync(dto, ct);
        return CompaniesContractMapper.ToContract(result);
    }

    public async Task<CompanyMembershipContract?> UpdateCompanyMembershipRoleAsync(
        Guid companyId,
        Guid membershipId,
        string role,
        CancellationToken ct = default)
    {
        var dto = await _companiesRepository.UpdateCompanyMembershipRoleAsync(companyId, membershipId, role, ct);
        return dto is { } ? CompaniesContractMapper.ToContract(dto) : null;
    }

    public async Task<CompanyMembershipContract?> DeactivateCompanyMembershipAsync(
        Guid companyId,
        Guid membershipId,
        CancellationToken ct = default)
    {
        var dto = await _companiesRepository.DeactivateCompanyMembershipAsync(companyId, membershipId, ct);
        return dto is { } ? CompaniesContractMapper.ToContract(dto) : null;
    }

    public async Task<CompanyUserMutationResultContract> UpdateCompanyMembershipRoleWithGuardsAsync(
        Guid companyId,
        Guid membershipId,
        string role,
        CancellationToken ct = default)
    {
        var membership = await _companiesRepository.GetCompanyMembershipAsync(companyId, membershipId, ct);
        if (membership == null)
        {
            return new CompanyUserMutationResultContract { Success = false, ErrorCode = "NOT_FOUND", ErrorMessage = "Membership not found." };
        }

        if (!membership.IsActive)
        {
            return new CompanyUserMutationResultContract { Success = false, ErrorCode = "MEMBERSHIP_INACTIVE", ErrorMessage = "Cannot edit inactive membership." };
        }

        if (Enum.TryParse<CompanyMembershipRole>(membership.Role, true, out var currentRole)
            && currentRole == CompanyMembershipRole.Owner
            && !string.Equals(role, CompanyMembershipRole.Owner.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var owners = await _companiesRepository.CountActiveCompanyOwnersAsync(companyId, ct);
            if (owners <= 1)
            {
                return new CompanyUserMutationResultContract { Success = false, ErrorCode = "LAST_OWNER_PROTECTION", ErrorMessage = "Cannot demote the last active owner." };
            }
        }

        var updated = await _companiesRepository.UpdateCompanyMembershipRoleAsync(companyId, membershipId, role, ct);
        if (updated == null)
        {
            return new CompanyUserMutationResultContract { Success = false, ErrorCode = "NOT_FOUND", ErrorMessage = "Membership not found." };
        }

        return new CompanyUserMutationResultContract { Success = true, Membership = CompaniesContractMapper.ToContract(updated) };
    }

    public async Task<CompanyUserMutationResultContract> DeactivateCompanyMembershipWithGuardsAsync(
        Guid companyId,
        Guid membershipId,
        CancellationToken ct = default)
    {
        var membership = await _companiesRepository.GetCompanyMembershipAsync(companyId, membershipId, ct);
        if (membership == null)
        {
            return new CompanyUserMutationResultContract { Success = false, ErrorCode = "NOT_FOUND", ErrorMessage = "Membership not found." };
        }

        if (Enum.TryParse<CompanyMembershipRole>(membership.Role, true, out var currentRole)
            && currentRole == CompanyMembershipRole.Owner
            && membership.IsActive)
        {
            var owners = await _companiesRepository.CountActiveCompanyOwnersAsync(companyId, ct);
            if (owners <= 1)
            {
                return new CompanyUserMutationResultContract { Success = false, ErrorCode = "LAST_OWNER_PROTECTION", ErrorMessage = "Cannot remove the last active owner." };
            }
        }

        var updated = await _companiesRepository.DeactivateCompanyMembershipAsync(companyId, membershipId, ct);
        if (updated == null)
        {
            return new CompanyUserMutationResultContract { Success = false, ErrorCode = "NOT_FOUND", ErrorMessage = "Membership not found." };
        }

        return new CompanyUserMutationResultContract { Success = true, Membership = CompaniesContractMapper.ToContract(updated) };
    }

    public async Task<AddCompanyUserResultContract> AddCompanyUserAsync(
        AddCompanyUserContract request,
        CancellationToken ct = default)
    {
        if (request.CompanyId == Guid.Empty)
        {
            return new AddCompanyUserResultContract { Success = false, ErrorCode = "VALIDATION", ErrorMessage = "Company identifier is required." };
        }

        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return new AddCompanyUserResultContract { Success = false, ErrorCode = "VALIDATION", ErrorMessage = "Email is required." };
        }

        if (!Enum.TryParse<CompanyMembershipRole>(request.Role, true, out _))
        {
            return new AddCompanyUserResultContract { Success = false, ErrorCode = "INVALID_ROLE", ErrorMessage = "Invalid company role." };
        }

        var existingUserId = await _mediator.Send(new GetUserIdByEmailQuery(email), ct);
        var isExistingUser = existingUserId.HasValue;
        var userId = existingUserId ?? Guid.Empty;

        if (!isExistingUser)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return new AddCompanyUserResultContract { Success = false, ErrorCode = "VALIDATION", ErrorMessage = "Password is required for new users." };
            }

            var registerResult = await _mediator.Send(new RegisterBasicUserCommand(email, request.Password), ct);

            if (!registerResult.Success)
            {
                return new AddCompanyUserResultContract
                {
                    Success = false,
                    ErrorCode = "USER_CREATE_FAILED",
                    ErrorMessage = registerResult.Errors.FirstOrDefault() ?? "Unable to create user."
                };
            }

            userId = registerResult.UserId;

            await _mediator.Send(new UpdateUserProfileCommand(
                userId,
                request.FirstName ?? string.Empty,
                request.LastName ?? string.Empty,
                request.PhoneNumber ?? string.Empty), ct);
        }

        var upsert = await _companiesRepository.UpsertCompanyMembershipAsync(
            CompaniesContractMapper.ToDto(new UpsertCompanyMembershipContract
            {
                CompanyId = request.CompanyId,
                UserId = userId,
                Role = request.Role
            }),
            ct);

        return new AddCompanyUserResultContract
        {
            Success = true,
            MembershipId = upsert.Membership.MembershipId,
            UserId = userId,
            Email = email,
            Role = upsert.Membership.Role,
            IsExistingUser = isExistingUser,
            AccessStatus = isExistingUser ? "existing_user_linked" : "new_user_created",
            NextAction = isExistingUser ? "User can sign in with existing credentials." : "User can sign in with the created password."
        };
    }

    public Task<int> CountActiveCompanyOwnersAsync(Guid companyId, CancellationToken ct = default)
    {
        return _companiesRepository.CountActiveCompanyOwnersAsync(companyId, ct);
    }

    public Task<bool> HasAnyActiveOwnerMembershipForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return _companiesRepository.HasAnyActiveOwnerMembershipForUserAsync(userId, ct);
    }

    public Task<bool> HasDeactivatedActiveMembershipAsync(Guid userId, CancellationToken ct = default)
    {
        return _companiesRepository.HasDeactivatedActiveMembershipAsync(userId, ct);
    }

    public async Task<IReadOnlyCollection<CompanyPromotionContract>> GetCompanyPromotionsAsync(
        Guid companyId,
        CancellationToken ct = default)
    {
        var promotions = await _companiesRepository.GetCompanyPromotionsAsync(companyId, ct);
        return promotions
            .Select(CompaniesContractMapper.ToContract)
            .ToList();
    }

    public async Task<CompanyPromotionContract?> GetCompanyPromotionAsync(
        Guid companyId,
        Guid promotionId,
        CancellationToken ct = default)
    {
        var dto = await _companiesRepository.GetCompanyPromotionAsync(companyId, promotionId, ct);
        return dto is { } ? CompaniesContractMapper.ToContract(dto) : null;
    }

    public async Task<PromotionOperationResultContract> CreateCompanyPromotionAsync(
        Guid companyId,
        UpsertCompanyPromotionContract request,
        CancellationToken ct = default)
    {
        var dto = CompaniesContractMapper.ToDto(request);
        var result = await _companiesRepository.CreateCompanyPromotionAsync(companyId, dto, ct);
        return CompaniesContractMapper.ToContract(result);
    }

    public async Task<PromotionOperationResultContract> UpdateCompanyPromotionAsync(
        Guid companyId,
        Guid promotionId,
        UpsertCompanyPromotionContract request,
        CancellationToken ct = default)
    {
        var dto = CompaniesContractMapper.ToDto(request);
        var result = await _companiesRepository.UpdateCompanyPromotionAsync(companyId, promotionId, dto, ct);
        return CompaniesContractMapper.ToContract(result);
    }

    public Task<bool> DeleteCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default)
    {
        return _companiesRepository.DeleteCompanyPromotionAsync(companyId, promotionId, ct);
    }

    public async Task<IReadOnlyCollection<CompanyPromotionContract>> GetSystemPromotionsAsync(
        CancellationToken ct = default)
    {
        var promotions = await _companiesRepository.GetSystemPromotionsAsync(ct);
        return promotions
            .Select(CompaniesContractMapper.ToContract)
            .ToList();
    }

    public async Task<CompanyPromotionContract?> GetSystemPromotionAsync(
        Guid promotionId,
        CancellationToken ct = default)
    {
        var dto = await _companiesRepository.GetSystemPromotionAsync(promotionId, ct);
        return dto is { } ? CompaniesContractMapper.ToContract(dto) : null;
    }

    public async Task<PromotionOperationResultContract> CreateSystemPromotionAsync(
        UpsertCompanyPromotionContract request,
        CancellationToken ct = default)
    {
        var dto = CompaniesContractMapper.ToDto(request);
        var result = await _companiesRepository.CreateSystemPromotionAsync(dto, ct);
        return CompaniesContractMapper.ToContract(result);
    }

    public async Task<PromotionOperationResultContract> UpdateSystemPromotionAsync(
        Guid promotionId,
        UpsertCompanyPromotionContract request,
        CancellationToken ct = default)
    {
        var dto = CompaniesContractMapper.ToDto(request);
        var result = await _companiesRepository.UpdateSystemPromotionAsync(promotionId, dto, ct);
        return CompaniesContractMapper.ToContract(result);
    }

    public Task<bool> DeleteSystemPromotionAsync(Guid promotionId, CancellationToken ct = default)
    {
        return _companiesRepository.DeleteSystemPromotionAsync(promotionId, ct);
    }

    public async Task<IReadOnlyCollection<UserPromotionContract>> GetUserPromotionsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var promotions = await _companiesRepository.GetUserPromotionsAsync(userId, ct);
        return promotions
            .Select(CompaniesContractMapper.ToContract)
            .ToList();
    }

    public async Task<PromotionOperationResultContract> RedeemPromotionAsync(
        Guid userId,
        string code,
        CancellationToken ct = default)
    {
        var result = await _companiesRepository.RedeemPromotionAsync(userId, code, ct);
        return CompaniesContractMapper.ToContract(result);
    }

    public Task<bool> RemoveUserPromotionAsync(
        Guid userId,
        Guid userPromotionId,
        CancellationToken ct = default)
    {
        return _companiesRepository.RemoveUserPromotionAsync(userId, userPromotionId, ct);
    }

    public async Task<UserPromotionContract?> GetValidUserPromotionByCodeAsync(
        Guid userId,
        string code,
        CancellationToken ct = default)
    {
        var dto = await _companiesRepository.GetValidUserPromotionByCodeAsync(userId, code, ct);
        return dto is { } ? CompaniesContractMapper.ToContract(dto) : null;
    }

    public async Task<IReadOnlyCollection<CompanyAuditEntryContract>> GetCompanyAuditAsync(
        Guid companyId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null,
        CancellationToken ct = default)
    {
        var entries = await _companiesRepository.GetCompanyAuditAsync(
            companyId,
            fromUtc,
            toUtc,
            entityName,
            action,
            ct);

        return entries
            .Select(CompaniesContractMapper.ToContract)
            .ToList();
    }

    public async Task LogAuditMutationAsync(
        Guid companyId,
        string userName,
        string entityName,
        Guid entityId,
        string action,
        string? changesJson = null,
        CancellationToken ct = default)
    {
        await _mediator.Publish(new AuditLogMutationRequestedNotification
        {
            CompanyId = companyId,
            UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim(),
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ChangesJson = changesJson,
            AtUtc = DateTime.UtcNow
        }, ct);
    }

    public async Task<CompanyAuditTrailContract> GetAuditTrailAsync(
        string entityName,
        Guid entityId,
        Guid? companyId = null,
        CancellationToken ct = default)
    {
        var trail = await _companiesRepository.GetAuditTrailAsync(entityName, entityId, companyId, ct);
        return CompaniesContractMapper.ToContract(trail);
    }

    public async Task<CreateCompanyWithOwnerMembershipResultContract> CreateCompanyWithOwnerMembershipAsync(
        CreateCompanyWithOwnerMembershipContract request,
        CancellationToken ct = default)
    {
        var dto = CompaniesContractMapper.ToDto(request);
        var result = await _companiesRepository.CreateCompanyWithOwnerMembershipAsync(dto, ct);
        return CompaniesContractMapper.ToContract(result);
    }
}
