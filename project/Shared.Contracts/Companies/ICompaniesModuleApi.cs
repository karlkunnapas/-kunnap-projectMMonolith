namespace Shared.Contracts.Companies;

public interface ICompaniesModuleApi
{
    Task<CompanyTenantContract?> GetCompanyTenantBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken ct = default);
    Task<bool> IsCompanyActiveAsync(Guid companyId, CancellationToken ct = default);
    Task<bool> HasActiveOwnerMembershipAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<AdminCompanyContract>> GetCompaniesForAdminAsync(string? search = null, CancellationToken ct = default);
    Task<AdminCompanyContract?> SetCompanyActivationAsync(Guid companyId, bool isActive, CancellationToken ct = default);
    Task<IReadOnlyCollection<UserCompanyMembershipContract>> GetUserCompaniesAsync(Guid userId, CancellationToken ct = default);
    Task<UserCompanyMembershipContract?> GetActiveCompanySelectionAsync(Guid userId, Guid companyId, CancellationToken ct = default);
    Task<UserCompanyMembershipContract?> SelectActiveCompanyAsync(Guid userId, Guid companyId, string actorUserName, CancellationToken ct = default);
    Task<bool> HasCompanyRoleAsync(Guid companyId, Guid userId, string minimumRole, CancellationToken ct = default);
    Task<IReadOnlyCollection<CompanyMembershipContract>> GetCompanyMembershipsAsync(Guid companyId, CancellationToken ct = default);
    Task<CompanyMembershipContract?> GetCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default);
    Task<CompanyMembershipContract?> GetCompanyMembershipByUserAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task<UpsertCompanyMembershipResultContract> UpsertCompanyMembershipAsync(UpsertCompanyMembershipContract request, CancellationToken ct = default);
    Task<CompanyMembershipContract?> UpdateCompanyMembershipRoleAsync(Guid companyId, Guid membershipId, string role, CancellationToken ct = default);
    Task<CompanyMembershipContract?> DeactivateCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default);
    Task<CompanyUserMutationResultContract> UpdateCompanyMembershipRoleWithGuardsAsync(Guid companyId, Guid membershipId, string role, CancellationToken ct = default);
    Task<CompanyUserMutationResultContract> DeactivateCompanyMembershipWithGuardsAsync(Guid companyId, Guid membershipId, CancellationToken ct = default);
    Task<AddCompanyUserResultContract> AddCompanyUserAsync(AddCompanyUserContract request, CancellationToken ct = default);
    Task<int> CountActiveCompanyOwnersAsync(Guid companyId, CancellationToken ct = default);
    Task<bool> HasAnyActiveOwnerMembershipForUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> HasDeactivatedActiveMembershipAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<CompanyPromotionContract>> GetCompanyPromotionsAsync(Guid companyId, CancellationToken ct = default);
    Task<CompanyPromotionContract?> GetCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default);
    Task<PromotionOperationResultContract> CreateCompanyPromotionAsync(Guid companyId, UpsertCompanyPromotionContract request, CancellationToken ct = default);
    Task<PromotionOperationResultContract> UpdateCompanyPromotionAsync(Guid companyId, Guid promotionId, UpsertCompanyPromotionContract request, CancellationToken ct = default);
    Task<bool> DeleteCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default);
    Task<IReadOnlyCollection<CompanyPromotionContract>> GetSystemPromotionsAsync(CancellationToken ct = default);
    Task<CompanyPromotionContract?> GetSystemPromotionAsync(Guid promotionId, CancellationToken ct = default);
    Task<PromotionOperationResultContract> CreateSystemPromotionAsync(UpsertCompanyPromotionContract request, CancellationToken ct = default);
    Task<PromotionOperationResultContract> UpdateSystemPromotionAsync(Guid promotionId, UpsertCompanyPromotionContract request, CancellationToken ct = default);
    Task<bool> DeleteSystemPromotionAsync(Guid promotionId, CancellationToken ct = default);
    Task<IReadOnlyCollection<UserPromotionContract>> GetUserPromotionsAsync(Guid userId, CancellationToken ct = default);
    Task<PromotionOperationResultContract> RedeemPromotionAsync(Guid userId, string code, CancellationToken ct = default);
    Task<bool> RemoveUserPromotionAsync(Guid userId, Guid userPromotionId, CancellationToken ct = default);
    Task<UserPromotionContract?> GetValidUserPromotionByCodeAsync(Guid userId, string code, CancellationToken ct = default);
    Task<IReadOnlyCollection<CompanyAuditEntryContract>> GetCompanyAuditAsync(
        Guid companyId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null,
        CancellationToken ct = default);
    Task LogAuditMutationAsync(
        Guid companyId,
        string userName,
        string entityName,
        Guid entityId,
        string action,
        string? changesJson = null,
        CancellationToken ct = default);
    Task<CompanyAuditTrailContract> GetAuditTrailAsync(
        string entityName,
        Guid entityId,
        Guid? companyId = null,
        CancellationToken ct = default);
    Task<CreateCompanyWithOwnerMembershipResultContract> CreateCompanyWithOwnerMembershipAsync(CreateCompanyWithOwnerMembershipContract request, CancellationToken ct = default);
}
