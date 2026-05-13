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

    public Task<CompanyTenantContract?> GetCompanyTenantBySlugAsync(string slug, CancellationToken ct = default) => _service.GetCompanyTenantBySlugAsync(slug, ct);
    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken ct = default) => _service.CompanyExistsAsync(companyId, ct);
    public Task<bool> IsCompanyActiveAsync(Guid companyId, CancellationToken ct = default) => _service.IsCompanyActiveAsync(companyId, ct);
    public Task<bool> HasActiveOwnerMembershipAsync(Guid companyId, Guid userId, CancellationToken ct = default) => _service.HasActiveOwnerMembershipAsync(companyId, userId, ct);
    public Task<IReadOnlyCollection<AdminCompanyContract>> GetCompaniesForAdminAsync(string? search = null, CancellationToken ct = default) => _service.GetCompaniesForAdminAsync(search, ct);
    public Task<AdminCompanyContract?> SetCompanyActivationAsync(Guid companyId, bool isActive, CancellationToken ct = default) => _service.SetCompanyActivationAsync(companyId, isActive, ct);
    public Task<IReadOnlyCollection<UserCompanyMembershipContract>> GetUserCompaniesAsync(Guid userId, CancellationToken ct = default) => _service.GetUserCompaniesAsync(userId, ct);
    public Task<UserCompanyMembershipContract?> GetActiveCompanySelectionAsync(Guid userId, Guid companyId, CancellationToken ct = default) => _service.GetActiveCompanySelectionAsync(userId, companyId, ct);
    public Task<IReadOnlyCollection<CompanyMembershipContract>> GetCompanyMembershipsAsync(Guid companyId, CancellationToken ct = default) => _service.GetCompanyMembershipsAsync(companyId, ct);
    public Task<CompanyMembershipContract?> GetCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default) => _service.GetCompanyMembershipAsync(companyId, membershipId, ct);
    public Task<CompanyMembershipContract?> GetCompanyMembershipByUserAsync(Guid companyId, Guid userId, CancellationToken ct = default) => _service.GetCompanyMembershipByUserAsync(companyId, userId, ct);
    public Task<UpsertCompanyMembershipResultContract> UpsertCompanyMembershipAsync(UpsertCompanyMembershipContract request, CancellationToken ct = default) => _service.UpsertCompanyMembershipAsync(request, ct);
    public Task<CompanyMembershipContract?> UpdateCompanyMembershipRoleAsync(Guid companyId, Guid membershipId, string role, CancellationToken ct = default) => _service.UpdateCompanyMembershipRoleAsync(companyId, membershipId, role, ct);
    public Task<CompanyMembershipContract?> DeactivateCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default) => _service.DeactivateCompanyMembershipAsync(companyId, membershipId, ct);
    public Task<int> CountActiveCompanyOwnersAsync(Guid companyId, CancellationToken ct = default) => _service.CountActiveCompanyOwnersAsync(companyId, ct);
    public Task<bool> HasAnyActiveOwnerMembershipForUserAsync(Guid userId, CancellationToken ct = default) => _service.HasAnyActiveOwnerMembershipForUserAsync(userId, ct);
    public Task<bool> HasDeactivatedActiveMembershipAsync(Guid userId, CancellationToken ct = default) => _service.HasDeactivatedActiveMembershipAsync(userId, ct);
    public Task<IReadOnlyCollection<CompanyPromotionContract>> GetCompanyPromotionsAsync(Guid companyId, CancellationToken ct = default) => _service.GetCompanyPromotionsAsync(companyId, ct);
    public Task<CompanyPromotionContract?> GetCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default) => _service.GetCompanyPromotionAsync(companyId, promotionId, ct);
    public Task<PromotionOperationResultContract> CreateCompanyPromotionAsync(Guid companyId, UpsertCompanyPromotionContract request, CancellationToken ct = default) => _service.CreateCompanyPromotionAsync(companyId, request, ct);
    public Task<PromotionOperationResultContract> UpdateCompanyPromotionAsync(Guid companyId, Guid promotionId, UpsertCompanyPromotionContract request, CancellationToken ct = default) => _service.UpdateCompanyPromotionAsync(companyId, promotionId, request, ct);
    public Task<bool> DeleteCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default) => _service.DeleteCompanyPromotionAsync(companyId, promotionId, ct);
    public Task<IReadOnlyCollection<UserPromotionContract>> GetUserPromotionsAsync(Guid userId, CancellationToken ct = default) => _service.GetUserPromotionsAsync(userId, ct);
    public Task<PromotionOperationResultContract> RedeemPromotionAsync(Guid userId, string code, CancellationToken ct = default) => _service.RedeemPromotionAsync(userId, code, ct);
    public Task<bool> RemoveUserPromotionAsync(Guid userId, Guid userPromotionId, CancellationToken ct = default) => _service.RemoveUserPromotionAsync(userId, userPromotionId, ct);
    public Task<UserPromotionContract?> GetValidUserPromotionByCodeAsync(Guid userId, string code, CancellationToken ct = default) => _service.GetValidUserPromotionByCodeAsync(userId, code, ct);
    public Task<IReadOnlyCollection<CompanyAuditEntryContract>> GetCompanyAuditAsync(Guid companyId, DateTime? fromUtc = null, DateTime? toUtc = null, string? entityName = null, string? action = null, CancellationToken ct = default) => _service.GetCompanyAuditAsync(companyId, fromUtc, toUtc, entityName, action, ct);
    public Task<CompanyAuditTrailContract> GetAuditTrailAsync(string entityName, Guid entityId, Guid? companyId = null, CancellationToken ct = default) => _service.GetAuditTrailAsync(entityName, entityId, companyId, ct);
    public Task<CreateCompanyWithOwnerMembershipResultContract> CreateCompanyWithOwnerMembershipAsync(CreateCompanyWithOwnerMembershipContract request, CancellationToken ct = default) => _service.CreateCompanyWithOwnerMembershipAsync(request, ct);
}
