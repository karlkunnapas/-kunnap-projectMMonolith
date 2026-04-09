using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IPromotionService
{
    Task<ServiceResult<List<PromotionSummaryDto>>> GetCompanyPromotionsAsync(Guid companyId);
    Task<ServiceResult<PromotionSummaryDto>> GetCompanyPromotionAsync(Guid companyId, Guid promotionId);
    Task<ServiceResult<PromotionSummaryDto>> CreateCompanyPromotionAsync(Guid companyId, PromotionUpsertDto dto);
    Task<ServiceResult<PromotionSummaryDto>> UpdateCompanyPromotionAsync(Guid companyId, Guid promotionId, PromotionUpsertDto dto);
    Task<ServiceResult> DeleteCompanyPromotionAsync(Guid companyId, Guid promotionId);
    Task<ServiceResult<List<UserPromotionDto>>> GetUserPromotionsAsync(Guid userId);
    Task<ServiceResult<UserPromotionDto>> RedeemPromotionAsync(Guid userId, string code);
    Task<ServiceResult> RemoveUserPromotionAsync(Guid userId, Guid userPromotionId);
    Task<ServiceResult<AppliedPromotionDto>> ValidateUserPromotionForCompanyAsync(Guid userId, Guid? companyId, string code);
    Task<ServiceResult<AppliedPromotionDto>> ValidateUserPromotionAsync(Guid userId, Guid stationId, string code);
}
