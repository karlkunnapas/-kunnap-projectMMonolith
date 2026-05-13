using System.Text.Json;
using Modules.Companies.Application.DTO;
using Shared.Contracts.Companies;

namespace Modules.Companies.Application.Mappers;

internal static class CompaniesContractMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static TTarget Map<TTarget>(object source)
    {
        var json = JsonSerializer.Serialize(source, JsonOptions);
        return JsonSerializer.Deserialize<TTarget>(json, JsonOptions)!;
    }

    public static UpsertCompanyMembershipDto ToDto(UpsertCompanyMembershipContract c) => Map<UpsertCompanyMembershipDto>(c);
    public static UpsertCompanyPromotionDto ToDto(UpsertCompanyPromotionContract c) => Map<UpsertCompanyPromotionDto>(c);
    public static CreateCompanyWithOwnerMembershipDto ToDto(CreateCompanyWithOwnerMembershipContract c) => Map<CreateCompanyWithOwnerMembershipDto>(c);

    public static AdminCompanyContract ToContract(AdminCompanyDto d) => Map<AdminCompanyContract>(d);
    public static CompanyTenantContract ToContract(CompanyTenantDto d) => Map<CompanyTenantContract>(d);
    public static UserCompanyMembershipContract ToContract(UserCompanyMembershipDto d) => Map<UserCompanyMembershipContract>(d);
    public static CompanyMembershipContract ToContract(CompanyMembershipDto d) => Map<CompanyMembershipContract>(d);
    public static UpsertCompanyMembershipResultContract ToContract(UpsertCompanyMembershipResultDto d) => Map<UpsertCompanyMembershipResultContract>(d);
    public static CompanyPromotionContract ToContract(CompanyPromotionDto d) => Map<CompanyPromotionContract>(d);
    public static PromotionOperationResultContract ToContract(PromotionOperationResultDto d) => Map<PromotionOperationResultContract>(d);
    public static UserPromotionContract ToContract(UserPromotionDto d) => Map<UserPromotionContract>(d);
    public static CompanyAuditEntryContract ToContract(CompanyAuditEntryDto d) => Map<CompanyAuditEntryContract>(d);
    public static CompanyAuditTrailContract ToContract(CompanyAuditTrailDto d) => Map<CompanyAuditTrailContract>(d);
    public static CreateCompanyWithOwnerMembershipResultContract ToContract(CreateCompanyWithOwnerMembershipResultDto d) => Map<CreateCompanyWithOwnerMembershipResultContract>(d);
}
