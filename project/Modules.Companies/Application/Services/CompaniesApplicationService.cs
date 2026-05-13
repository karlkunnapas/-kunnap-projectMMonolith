using Modules.Companies.Application.Mappers;
using Modules.Companies.Infrastructure;
using Shared.Contracts.Companies;

namespace Modules.Companies.Application.Services;

internal sealed class CompaniesApplicationService : ICompaniesApplicationService
{
    private readonly ICompaniesRepository _companiesRepository;

    public CompaniesApplicationService(ICompaniesRepository companiesRepository)
    {
        _companiesRepository = companiesRepository;
    }

    public async Task<CompanyTenantContract?> GetCompanyTenantBySlugAsync(string slug, CancellationToken ct = default) => (await _companiesRepository.GetCompanyTenantBySlugAsync(slug, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken ct = default) => _companiesRepository.CompanyExistsAsync(companyId, ct);
    public Task<bool> IsCompanyActiveAsync(Guid companyId, CancellationToken ct = default) => _companiesRepository.IsCompanyActiveAsync(companyId, ct);
    public Task<bool> HasActiveOwnerMembershipAsync(Guid companyId, Guid userId, CancellationToken ct = default) => _companiesRepository.HasActiveOwnerMembershipAsync(companyId, userId, ct);
    public async Task<IReadOnlyCollection<AdminCompanyContract>> GetCompaniesForAdminAsync(string? search = null, CancellationToken ct = default) => (await _companiesRepository.GetCompaniesForAdminAsync(search, ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<AdminCompanyContract?> SetCompanyActivationAsync(Guid companyId, bool isActive, CancellationToken ct = default) => (await _companiesRepository.SetCompanyActivationAsync(companyId, isActive, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<IReadOnlyCollection<UserCompanyMembershipContract>> GetUserCompaniesAsync(Guid userId, CancellationToken ct = default) => (await _companiesRepository.GetUserCompaniesAsync(userId, ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<UserCompanyMembershipContract?> GetActiveCompanySelectionAsync(Guid userId, Guid companyId, CancellationToken ct = default) => (await _companiesRepository.GetActiveCompanySelectionAsync(userId, companyId, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<IReadOnlyCollection<CompanyMembershipContract>> GetCompanyMembershipsAsync(Guid companyId, CancellationToken ct = default) => (await _companiesRepository.GetCompanyMembershipsAsync(companyId, ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<CompanyMembershipContract?> GetCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default) => (await _companiesRepository.GetCompanyMembershipAsync(companyId, membershipId, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<CompanyMembershipContract?> GetCompanyMembershipByUserAsync(Guid companyId, Guid userId, CancellationToken ct = default) => (await _companiesRepository.GetCompanyMembershipByUserAsync(companyId, userId, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<UpsertCompanyMembershipResultContract> UpsertCompanyMembershipAsync(UpsertCompanyMembershipContract request, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.UpsertCompanyMembershipAsync(CompaniesContractMapper.ToDto(request), ct));
    public async Task<CompanyMembershipContract?> UpdateCompanyMembershipRoleAsync(Guid companyId, Guid membershipId, string role, CancellationToken ct = default) => (await _companiesRepository.UpdateCompanyMembershipRoleAsync(companyId, membershipId, role, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<CompanyMembershipContract?> DeactivateCompanyMembershipAsync(Guid companyId, Guid membershipId, CancellationToken ct = default) => (await _companiesRepository.DeactivateCompanyMembershipAsync(companyId, membershipId, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public Task<int> CountActiveCompanyOwnersAsync(Guid companyId, CancellationToken ct = default) => _companiesRepository.CountActiveCompanyOwnersAsync(companyId, ct);
    public Task<bool> HasAnyActiveOwnerMembershipForUserAsync(Guid userId, CancellationToken ct = default) => _companiesRepository.HasAnyActiveOwnerMembershipForUserAsync(userId, ct);
    public Task<bool> HasDeactivatedActiveMembershipAsync(Guid userId, CancellationToken ct = default) => _companiesRepository.HasDeactivatedActiveMembershipAsync(userId, ct);
    public async Task<IReadOnlyCollection<CompanyPromotionContract>> GetCompanyPromotionsAsync(Guid companyId, CancellationToken ct = default) => (await _companiesRepository.GetCompanyPromotionsAsync(companyId, ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<CompanyPromotionContract?> GetCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default) => (await _companiesRepository.GetCompanyPromotionAsync(companyId, promotionId, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<PromotionOperationResultContract> CreateCompanyPromotionAsync(Guid companyId, UpsertCompanyPromotionContract request, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.CreateCompanyPromotionAsync(companyId, CompaniesContractMapper.ToDto(request), ct));
    public async Task<PromotionOperationResultContract> UpdateCompanyPromotionAsync(Guid companyId, Guid promotionId, UpsertCompanyPromotionContract request, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.UpdateCompanyPromotionAsync(companyId, promotionId, CompaniesContractMapper.ToDto(request), ct));
    public Task<bool> DeleteCompanyPromotionAsync(Guid companyId, Guid promotionId, CancellationToken ct = default) => _companiesRepository.DeleteCompanyPromotionAsync(companyId, promotionId, ct);
    public async Task<IReadOnlyCollection<UserPromotionContract>> GetUserPromotionsAsync(Guid userId, CancellationToken ct = default) => (await _companiesRepository.GetUserPromotionsAsync(userId, ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<PromotionOperationResultContract> RedeemPromotionAsync(Guid userId, string code, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.RedeemPromotionAsync(userId, code, ct));
    public Task<bool> RemoveUserPromotionAsync(Guid userId, Guid userPromotionId, CancellationToken ct = default) => _companiesRepository.RemoveUserPromotionAsync(userId, userPromotionId, ct);
    public async Task<UserPromotionContract?> GetValidUserPromotionByCodeAsync(Guid userId, string code, CancellationToken ct = default) => (await _companiesRepository.GetValidUserPromotionByCodeAsync(userId, code, ct)) is { } dto ? CompaniesContractMapper.ToContract(dto) : null;
    public async Task<IReadOnlyCollection<CompanyAuditEntryContract>> GetCompanyAuditAsync(Guid companyId, DateTime? fromUtc = null, DateTime? toUtc = null, string? entityName = null, string? action = null, CancellationToken ct = default) => (await _companiesRepository.GetCompanyAuditAsync(companyId, fromUtc, toUtc, entityName, action, ct)).Select(CompaniesContractMapper.ToContract).ToList();
    public async Task<CompanyAuditTrailContract> GetAuditTrailAsync(string entityName, Guid entityId, Guid? companyId = null, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.GetAuditTrailAsync(entityName, entityId, companyId, ct));
    public async Task<CreateCompanyWithOwnerMembershipResultContract> CreateCompanyWithOwnerMembershipAsync(CreateCompanyWithOwnerMembershipContract request, CancellationToken ct = default) => CompaniesContractMapper.ToContract(await _companiesRepository.CreateCompanyWithOwnerMembershipAsync(CompaniesContractMapper.ToDto(request), ct));
}
