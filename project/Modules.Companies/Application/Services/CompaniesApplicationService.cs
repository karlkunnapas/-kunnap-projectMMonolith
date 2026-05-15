using Modules.Companies.Application.Mappers;
using Modules.Companies.Infrastructure;
using Mediator;
using Shared.Contracts.Companies;
using Shared.Contracts.Companies.Events;
using Shared.Contracts.Users;

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
    private readonly IUsersModuleApi _usersModuleApi;
    private readonly IMediator _mediator;

    public CompaniesApplicationService(ICompaniesRepository companiesRepository, IUsersModuleApi usersModuleApi, IMediator mediator)
    {
        _companiesRepository = companiesRepository;
        _usersModuleApi = usersModuleApi;
        _mediator = mediator;
    }

    public async Task<CompanyTenantContract?> GetCompanyTenantBySlugAsync(string slug, CancellationToken ct = default) => (await _companiesRepository.GetCompanyTenantBySlugAsync(slug, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken ct = default) => _companiesRepository.CompanyExistsAsync(companyId, ct);
    public Task<bool> IsCompanyActiveAsync(Guid companyId, CancellationToken ct = default) => _companiesRepository.IsCompanyActiveAsync(companyId, ct);
    public Task<bool> HasActiveOwnerMembershipAsync(Guid companyId, Guid userId, CancellationToken ct = default) => _companiesRepository.HasActiveOwnerMembershipAsync(companyId, userId, ct);
    public async Task<IReadOnlyCollection<AdminCompanyContract>> GetCompaniesForAdminAsync(string? search = null, CancellationToken ct = default) => (await _companiesRepository.GetCompaniesForAdminAsync(search, ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<AdminCompanyContract?> SetCompanyActivationAsync(Guid companyId, bool isActive, CancellationToken ct = default) => (await _companiesRepository.SetCompanyActivationAsync(companyId, isActive, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<IReadOnlyCollection<UserCompanyMembershipContract>> GetUserCompaniesAsync(Guid userId, CancellationToken ct = default) => (await _companiesRepository.GetUserCompaniesAsync(userId, ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<UserCompanyMembershipContract?> GetActiveCompanySelectionAsync(Guid userId, Guid companyId, CancellationToken ct = default) => (await _companiesRepository.GetActiveCompanySelectionAsync(userId, companyId, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<UserCompanyMembershipContract?> SelectActiveCompanyAsync(Guid userId, Guid companyId, string actorUserName, CancellationToken ct = default)
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
    public async Task<bool> HasCompanyRoleAsync(Guid companyId, Guid userId, string minimumRole, CancellationToken ct = default)
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

    public async Task<IReadOnlyCollection<CompanyMembershipContract>> GetCompanyMembershipsAsync(Guid companyId, CancellationToken ct = default) => (await _companiesRepository.GetCompanyMembershipsAsync(companyId, ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<CompanyMembershipContract?> GetCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default) => (await _companiesRepository.GetCompanyMembershipAsync(companyId, membershipId, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<CompanyMembershipContract?> GetCompanyMembershipByUserAsync(Guid companyId, Guid userId, CancellationToken ct = default) => (await _companiesRepository.GetCompanyMembershipByUserAsync(companyId, userId, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<UpsertCompanyMembershipResultContract> UpsertCompanyMembershipAsync(UpsertCompanyMembershipContract request, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.UpsertCompanyMembershipAsync(CompaniesContractMapper.ToDto(request), ct));
    public async Task<CompanyMembershipContract?> UpdateCompanyMembershipRoleAsync(Guid companyId, Guid membershipId, string role, CancellationToken ct = default) => (await _companiesRepository.UpdateCompanyMembershipRoleAsync(companyId, membershipId, role, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<CompanyMembershipContract?> DeactivateCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default) => (await _companiesRepository.DeactivateCompanyMembershipAsync(companyId, membershipId, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<CompanyUserMutationResultContract> UpdateCompanyMembershipRoleWithGuardsAsync(Guid companyId, Guid membershipId, string role, CancellationToken ct = default)
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

    public async Task<CompanyUserMutationResultContract> DeactivateCompanyMembershipWithGuardsAsync(Guid companyId, Guid membershipId, CancellationToken ct = default)
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

    public async Task<AddCompanyUserResultContract> AddCompanyUserAsync(AddCompanyUserContract request, CancellationToken ct = default)
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

        var existingUserId = await _usersModuleApi.GetUserIdByEmailAsync(email, ct);
        var isExistingUser = existingUserId.HasValue;
        var userId = existingUserId ?? Guid.Empty;

        if (!isExistingUser)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return new AddCompanyUserResultContract { Success = false, ErrorCode = "VALIDATION", ErrorMessage = "Password is required for new users." };
            }

            var registerResult = await _usersModuleApi.RegisterBasicUserAsync(new RegisterBasicUserContract
            {
                Email = email,
                Password = request.Password
            }, ct);

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

            await _usersModuleApi.UpdateUserProfileAsync(userId, new UpdateUserProfileContract
            {
                FirstName = request.FirstName ?? string.Empty,
                LastName = request.LastName ?? string.Empty,
                PhoneNumber = request.PhoneNumber ?? string.Empty
            }, ct);
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

    public Task<int> CountActiveCompanyOwnersAsync(Guid companyId, CancellationToken ct = default) => _companiesRepository.CountActiveCompanyOwnersAsync(companyId, ct);
    public Task<bool> HasAnyActiveOwnerMembershipForUserAsync(Guid userId, CancellationToken ct = default) => _companiesRepository.HasAnyActiveOwnerMembershipForUserAsync(userId, ct);
    public Task<bool> HasDeactivatedActiveMembershipAsync(Guid userId, CancellationToken ct = default) => _companiesRepository.HasDeactivatedActiveMembershipAsync(userId, ct);
    public async Task<IReadOnlyCollection<CompanyPromotionContract>> GetCompanyPromotionsAsync(Guid companyId, CancellationToken ct = default) => (await _companiesRepository.GetCompanyPromotionsAsync(companyId, ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<CompanyPromotionContract?> GetCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default) => (await _companiesRepository.GetCompanyPromotionAsync(companyId, promotionId, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<PromotionOperationResultContract> CreateCompanyPromotionAsync(Guid companyId, UpsertCompanyPromotionContract request, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.CreateCompanyPromotionAsync(companyId, CompaniesContractMapper.ToDto(request), ct));
    public async Task<PromotionOperationResultContract> UpdateCompanyPromotionAsync(Guid companyId, Guid promotionId, UpsertCompanyPromotionContract request, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.UpdateCompanyPromotionAsync(companyId, promotionId, CompaniesContractMapper.ToDto(request), ct));
    public Task<bool> DeleteCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default) => _companiesRepository.DeleteCompanyPromotionAsync(companyId, promotionId, ct);
    public async Task<IReadOnlyCollection<CompanyPromotionContract>> GetSystemPromotionsAsync(CancellationToken ct = default) => (await _companiesRepository.GetSystemPromotionsAsync(ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<CompanyPromotionContract?> GetSystemPromotionAsync(Guid promotionId, CancellationToken ct = default) => (await _companiesRepository.GetSystemPromotionAsync(promotionId, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<PromotionOperationResultContract> CreateSystemPromotionAsync(UpsertCompanyPromotionContract request, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.CreateSystemPromotionAsync(CompaniesContractMapper.ToDto(request), ct));
    public async Task<PromotionOperationResultContract> UpdateSystemPromotionAsync(Guid promotionId, UpsertCompanyPromotionContract request, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.UpdateSystemPromotionAsync(promotionId, CompaniesContractMapper.ToDto(request), ct));
    public Task<bool> DeleteSystemPromotionAsync(Guid promotionId, CancellationToken ct = default) => _companiesRepository.DeleteSystemPromotionAsync(promotionId, ct);
    public async Task<IReadOnlyCollection<UserPromotionContract>> GetUserPromotionsAsync(Guid userId, CancellationToken ct = default) => (await _companiesRepository.GetUserPromotionsAsync(userId, ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<PromotionOperationResultContract> RedeemPromotionAsync(Guid userId, string code, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.RedeemPromotionAsync(userId, code, ct));
    public Task<bool> RemoveUserPromotionAsync(Guid userId, Guid userPromotionId, CancellationToken ct = default) => _companiesRepository.RemoveUserPromotionAsync(userId, userPromotionId, ct);
    public async Task<UserPromotionContract?> GetValidUserPromotionByCodeAsync(Guid userId, string code, CancellationToken ct = default) => (await _companiesRepository.GetValidUserPromotionByCodeAsync(userId, code, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<IReadOnlyCollection<CompanyAuditEntryContract>> GetCompanyAuditAsync(Guid companyId, DateTime? fromUtc = null, DateTime? toUtc = null, string? entityName = null, string? action = null, CancellationToken ct = default) => (await _companiesRepository.GetCompanyAuditAsync(companyId, fromUtc, toUtc, entityName, action, ct)).Select(CompaniesContractMapper.ToContract).ToList();
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

    public async Task<CompanyAuditTrailContract> GetAuditTrailAsync(string entityName, Guid entityId, Guid? companyId = null, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.GetAuditTrailAsync(entityName, entityId, companyId, ct));
    public async Task<CreateCompanyWithOwnerMembershipResultContract> CreateCompanyWithOwnerMembershipAsync(CreateCompanyWithOwnerMembershipContract request, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.CreateCompanyWithOwnerMembershipAsync(CompaniesContractMapper.ToDto(request), ct));
}
