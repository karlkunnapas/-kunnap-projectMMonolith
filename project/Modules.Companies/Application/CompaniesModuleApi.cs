using Modules.Companies.Application.Services;
using Shared.Contracts.Companies;

namespace Modules.Companies.Application;

internal sealed class CompaniesModuleApi : ICompaniesModuleApi
{
    private readonly ICompaniesApplicationService _service;

    public CompaniesModuleApi(ICompaniesApplicationService service)
    {
        _service = service;
    }

    public Task<CompanyTenantContract?> GetCompanyTenantBySlugAsync(string slug, CancellationToken ct = default)
    {
        return _service.GetCompanyTenantBySlugAsync(slug, ct);
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken ct = default)
    {
        return _service.CompanyExistsAsync(companyId, ct);
    }

    public Task<bool> IsCompanyActiveAsync(Guid companyId, CancellationToken ct = default)
    {
        return _service.IsCompanyActiveAsync(companyId, ct);
    }

    public Task<bool> HasActiveOwnerMembershipAsync(
        Guid companyId,
        Guid userId,
        CancellationToken ct = default)
    {
        return _service.HasActiveOwnerMembershipAsync(companyId, userId, ct);
    }

    public Task<IReadOnlyCollection<AdminCompanyContract>> GetCompaniesForAdminAsync(
        string? search = null,
        CancellationToken ct = default)
    {
        return _service.GetCompaniesForAdminAsync(search, ct);
    }

    public Task<AdminCompanyContract?> SetCompanyActivationAsync(
        Guid companyId,
        bool isActive,
        CancellationToken ct = default)
    {
        return _service.SetCompanyActivationAsync(companyId, isActive, ct);
    }

    public Task<IReadOnlyCollection<UserCompanyMembershipContract>> GetUserCompaniesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return _service.GetUserCompaniesAsync(userId, ct);
    }

    public Task<UserCompanyMembershipContract?> GetActiveCompanySelectionAsync(
        Guid userId,
        Guid companyId,
        CancellationToken ct = default)
    {
        return _service.GetActiveCompanySelectionAsync(userId, companyId, ct);
    }

    public Task<UserCompanyMembershipContract?> SelectActiveCompanyAsync(
        Guid userId,
        Guid companyId,
        string actorUserName,
        CancellationToken ct = default)
    {
        return _service.SelectActiveCompanyAsync(userId, companyId, actorUserName, ct);
    }

    public Task<bool> HasCompanyRoleAsync(
        Guid companyId,
        Guid userId,
        string minimumRole,
        CancellationToken ct = default)
    {
        return _service.HasCompanyRoleAsync(companyId, userId, minimumRole, ct);
    }

    public Task<IReadOnlyCollection<CompanyMembershipContract>> GetCompanyMembershipsAsync(
        Guid companyId,
        CancellationToken ct = default)
    {
        return _service.GetCompanyMembershipsAsync(companyId, ct);
    }

    public Task<CompanyMembershipContract?> GetCompanyMembershipAsync(
        Guid companyId,
        Guid membershipId,
        CancellationToken ct = default)
    {
        return _service.GetCompanyMembershipAsync(companyId, membershipId, ct);
    }

    public Task<CompanyMembershipContract?> GetCompanyMembershipByUserAsync(
        Guid companyId,
        Guid userId,
        CancellationToken ct = default)
    {
        return _service.GetCompanyMembershipByUserAsync(companyId, userId, ct);
    }

    public Task<UpsertCompanyMembershipResultContract> UpsertCompanyMembershipAsync(
        UpsertCompanyMembershipContract request,
        CancellationToken ct = default)
    {
        return _service.UpsertCompanyMembershipAsync(request, ct);
    }

    public Task<CompanyMembershipContract?> UpdateCompanyMembershipRoleAsync(
        Guid companyId,
        Guid membershipId,
        string role,
        CancellationToken ct = default)
    {
        return _service.UpdateCompanyMembershipRoleAsync(companyId, membershipId, role, ct);
    }

    public Task<CompanyMembershipContract?> DeactivateCompanyMembershipAsync(
        Guid companyId,
        Guid membershipId,
        CancellationToken ct = default)
    {
        return _service.DeactivateCompanyMembershipAsync(companyId, membershipId, ct);
    }

    public Task<CompanyUserMutationResultContract> UpdateCompanyMembershipRoleWithGuardsAsync(
        Guid companyId,
        Guid membershipId,
        string role,
        CancellationToken ct = default)
    {
        return _service.UpdateCompanyMembershipRoleWithGuardsAsync(companyId, membershipId, role, ct);
    }

    public Task<CompanyUserMutationResultContract> DeactivateCompanyMembershipWithGuardsAsync(
        Guid companyId,
        Guid membershipId,
        CancellationToken ct = default)
    {
        return _service.DeactivateCompanyMembershipWithGuardsAsync(companyId, membershipId, ct);
    }

    public Task<AddCompanyUserResultContract> AddCompanyUserAsync(
        AddCompanyUserContract request,
        CancellationToken ct = default)
    {
        return _service.AddCompanyUserAsync(request, ct);
    }

    public Task<int> CountActiveCompanyOwnersAsync(Guid companyId, CancellationToken ct = default)
    {
        return _service.CountActiveCompanyOwnersAsync(companyId, ct);
    }

    public Task<bool> HasAnyActiveOwnerMembershipForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return _service.HasAnyActiveOwnerMembershipForUserAsync(userId, ct);
    }

    public Task<bool> HasDeactivatedActiveMembershipAsync(Guid userId, CancellationToken ct = default)
    {
        return _service.HasDeactivatedActiveMembershipAsync(userId, ct);
    }

    public Task<IReadOnlyCollection<CompanyPromotionContract>> GetCompanyPromotionsAsync(
        Guid companyId,
        CancellationToken ct = default)
    {
        return _service.GetCompanyPromotionsAsync(companyId, ct);
    }

    public Task<CompanyPromotionContract?> GetCompanyPromotionAsync(
        Guid companyId,
        Guid promotionId,
        CancellationToken ct = default)
    {
        return _service.GetCompanyPromotionAsync(companyId, promotionId, ct);
    }

    public Task<PromotionOperationResultContract> CreateCompanyPromotionAsync(
        Guid companyId,
        UpsertCompanyPromotionContract request,
        CancellationToken ct = default)
    {
        return _service.CreateCompanyPromotionAsync(companyId, request, ct);
    }

    public Task<PromotionOperationResultContract> UpdateCompanyPromotionAsync(
        Guid companyId,
        Guid promotionId,
        UpsertCompanyPromotionContract request,
        CancellationToken ct = default)
    {
        return _service.UpdateCompanyPromotionAsync(companyId, promotionId, request, ct);
    }

    public Task<bool> DeleteCompanyPromotionAsync(
        Guid companyId,
        Guid promotionId,
        CancellationToken ct = default)
    {
        return _service.DeleteCompanyPromotionAsync(companyId, promotionId, ct);
    }

    public Task<IReadOnlyCollection<CompanyPromotionContract>> GetSystemPromotionsAsync(CancellationToken ct = default)
    {
        return _service.GetSystemPromotionsAsync(ct);
    }

    public Task<CompanyPromotionContract?> GetSystemPromotionAsync(Guid promotionId, CancellationToken ct = default)
    {
        return _service.GetSystemPromotionAsync(promotionId, ct);
    }

    public Task<PromotionOperationResultContract> CreateSystemPromotionAsync(
        UpsertCompanyPromotionContract request,
        CancellationToken ct = default)
    {
        return _service.CreateSystemPromotionAsync(request, ct);
    }

    public Task<PromotionOperationResultContract> UpdateSystemPromotionAsync(
        Guid promotionId,
        UpsertCompanyPromotionContract request,
        CancellationToken ct = default)
    {
        return _service.UpdateSystemPromotionAsync(promotionId, request, ct);
    }

    public Task<bool> DeleteSystemPromotionAsync(Guid promotionId, CancellationToken ct = default)
    {
        return _service.DeleteSystemPromotionAsync(promotionId, ct);
    }

    public Task<IReadOnlyCollection<UserPromotionContract>> GetUserPromotionsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return _service.GetUserPromotionsAsync(userId, ct);
    }

    public Task<PromotionOperationResultContract> RedeemPromotionAsync(
        Guid userId,
        string code,
        CancellationToken ct = default)
    {
        return _service.RedeemPromotionAsync(userId, code, ct);
    }

    public Task<bool> RemoveUserPromotionAsync(
        Guid userId,
        Guid userPromotionId,
        CancellationToken ct = default)
    {
        return _service.RemoveUserPromotionAsync(userId, userPromotionId, ct);
    }

    public Task<UserPromotionContract?> GetValidUserPromotionByCodeAsync(
        Guid userId,
        string code,
        CancellationToken ct = default)
    {
        return _service.GetValidUserPromotionByCodeAsync(userId, code, ct);
    }

    public Task<IReadOnlyCollection<CompanyAuditEntryContract>> GetCompanyAuditAsync(
        Guid companyId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null,
        CancellationToken ct = default)
    {
        return _service.GetCompanyAuditAsync(companyId, fromUtc, toUtc, entityName, action, ct);
    }

    public Task LogAuditMutationAsync(
        Guid companyId,
        string userName,
        string entityName,
        Guid entityId,
        string action,
        string? changesJson = null,
        CancellationToken ct = default)
    {
        return _service.LogAuditMutationAsync(companyId, userName, entityName, entityId, action, changesJson, ct);
    }

    public Task<CompanyAuditTrailContract> GetAuditTrailAsync(
        string entityName,
        Guid entityId,
        Guid? companyId = null,
        CancellationToken ct = default)
    {
        return _service.GetAuditTrailAsync(entityName, entityId, companyId, ct);
    }

    public Task<CreateCompanyWithOwnerMembershipResultContract> CreateCompanyWithOwnerMembershipAsync(
        CreateCompanyWithOwnerMembershipContract request,
        CancellationToken ct = default)
    {
        return _service.CreateCompanyWithOwnerMembershipAsync(request, ct);
    }
}
