using Modules.Companies.Application.DTO;

namespace Modules.Companies.Infrastructure;

internal interface ICompaniesRepository
{
    Task<CompanyTenantDto?> GetCompanyTenantBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken ct = default);
    Task<bool> IsCompanyActiveAsync(Guid companyId, CancellationToken ct = default);
    Task<bool> HasActiveOwnerMembershipAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<AdminCompanyDto>> GetCompaniesForAdminAsync(string? search = null, CancellationToken ct = default);
    Task<AdminCompanyDto?> SetCompanyActivationAsync(Guid companyId, bool isActive, CancellationToken ct = default);
    Task<IReadOnlyCollection<UserCompanyMembershipDto>> GetUserCompaniesAsync(Guid userId, CancellationToken ct = default);
    Task<UserCompanyMembershipDto?> GetActiveCompanySelectionAsync(Guid userId, Guid companyId, CancellationToken ct = default);
    Task<IReadOnlyCollection<CompanyMembershipDto>> GetCompanyMembershipsAsync(Guid companyId, CancellationToken ct = default);
    Task<CompanyMembershipDto?> GetCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default);
    Task<CompanyMembershipDto?> GetCompanyMembershipByUserAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task<UpsertCompanyMembershipResultDto> UpsertCompanyMembershipAsync(UpsertCompanyMembershipDto request, CancellationToken ct = default);
    Task<CompanyMembershipDto?> UpdateCompanyMembershipRoleAsync(Guid companyId, Guid membershipId, string role, CancellationToken ct = default);
    Task<CompanyMembershipDto?> DeactivateCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default);
    Task<int> CountActiveCompanyOwnersAsync(Guid companyId, CancellationToken ct = default);
    Task<bool> HasAnyActiveOwnerMembershipForUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> HasDeactivatedActiveMembershipAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<CompanyPromotionDto>> GetCompanyPromotionsAsync(Guid companyId, CancellationToken ct = default);
    Task<CompanyPromotionDto?> GetCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default);
    Task<PromotionOperationResultDto> CreateCompanyPromotionAsync(Guid companyId, UpsertCompanyPromotionDto request, CancellationToken ct = default);
    Task<PromotionOperationResultDto> UpdateCompanyPromotionAsync(Guid companyId, Guid promotionId, UpsertCompanyPromotionDto request, CancellationToken ct = default);
    Task<bool> DeleteCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default);
    Task<IReadOnlyCollection<UserPromotionDto>> GetUserPromotionsAsync(Guid userId, CancellationToken ct = default);
    Task<PromotionOperationResultDto> RedeemPromotionAsync(Guid userId, string code, CancellationToken ct = default);
    Task<bool> RemoveUserPromotionAsync(Guid userId, Guid userPromotionId, CancellationToken ct = default);
    Task<UserPromotionDto?> GetValidUserPromotionByCodeAsync(Guid userId, string code, CancellationToken ct = default);
    Task<IReadOnlyCollection<CompanyAuditEntryDto>> GetCompanyAuditAsync(
        Guid companyId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? entityName = null,
        string? action = null,
        CancellationToken ct = default);
    Task<CompanyAuditTrailDto> GetAuditTrailAsync(
        string entityName,
        Guid entityId,
        Guid? companyId = null,
        CancellationToken ct = default);
    Task<CreateCompanyWithOwnerMembershipResultDto> CreateCompanyWithOwnerMembershipAsync(CreateCompanyWithOwnerMembershipDto request, CancellationToken ct = default);
}
