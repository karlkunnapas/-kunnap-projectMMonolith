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

    public static UpsertCompanyMembershipDto ToDto(UpsertCompanyMembershipContract c)
    {
        return Map<UpsertCompanyMembershipDto>(c);
    }

    public static UpsertCompanyPromotionDto ToDto(UpsertCompanyPromotionContract c)
    {
        return Map<UpsertCompanyPromotionDto>(c);
    }

    public static CreateCompanyWithOwnerMembershipDto ToDto(CreateCompanyWithOwnerMembershipContract c)
    {
        return Map<CreateCompanyWithOwnerMembershipDto>(c);
    }

    public static AdminCompanyContract ToContract(AdminCompanyDto d)
    {
        return Map<AdminCompanyContract>(d);
    }

    public static CompanyTenantContract ToContract(CompanyTenantDto d)
    {
        return Map<CompanyTenantContract>(d);
    }

    public static UserCompanyMembershipContract ToContract(UserCompanyMembershipDto d)
    {
        return Map<UserCompanyMembershipContract>(d);
    }

    public static CompanyMembershipContract ToContract(CompanyMembershipDto d)
    {
        return Map<CompanyMembershipContract>(d);
    }

    public static UpsertCompanyMembershipResultContract ToContract(UpsertCompanyMembershipResultDto d)
    {
        return Map<UpsertCompanyMembershipResultContract>(d);
    }

    public static CompanyPromotionContract ToContract(CompanyPromotionDto d)
    {
        return Map<CompanyPromotionContract>(d);
    }

    public static PromotionOperationResultContract ToContract(PromotionOperationResultDto d)
    {
        return Map<PromotionOperationResultContract>(d);
    }

    public static UserPromotionContract ToContract(UserPromotionDto d)
    {
        return Map<UserPromotionContract>(d);
    }

    public static CompanyAuditEntryContract ToContract(CompanyAuditEntryDto d)
    {
        return Map<CompanyAuditEntryContract>(d);
    }

    public static CompanyAuditTrailContract ToContract(CompanyAuditTrailDto d)
    {
        return Map<CompanyAuditTrailContract>(d);
    }

    public static CreateCompanyWithOwnerMembershipResultContract ToContract(CreateCompanyWithOwnerMembershipResultDto d)
    {
        return Map<CreateCompanyWithOwnerMembershipResultContract>(d);
    }
}
