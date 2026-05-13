using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using Shared.Contracts.Charging;
using Shared.Contracts.Companies;

namespace App.BLL.Services;

public class PromotionService : IPromotionService
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;

    public PromotionService(IChargingModuleApi chargingModuleApi, ICompaniesModuleApi companiesModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
        _companiesModuleApi = companiesModuleApi;
    }

    public async Task<ServiceResult<List<PromotionSummaryDto>>> GetCompanyPromotionsAsync(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<PromotionSummaryDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var promotions = await _companiesModuleApi.GetCompanyPromotionsAsync(companyId);
        return ServiceResult<List<PromotionSummaryDto>>.Ok(promotions.Select(MapPromotion).ToList());
    }

    public async Task<ServiceResult<PromotionSummaryDto>> GetCompanyPromotionAsync(Guid companyId, Guid promotionId)
    {
        if (companyId == Guid.Empty || promotionId == Guid.Empty)
        {
            return ServiceResult<PromotionSummaryDto>.Fail("VALIDATION", "Company id and promotion id are required.");
        }

        var promotion = await _companiesModuleApi.GetCompanyPromotionAsync(companyId, promotionId);
        return promotion == null
            ? ServiceResult<PromotionSummaryDto>.Fail("FORBIDDEN", "Promotion not found or access denied.")
            : ServiceResult<PromotionSummaryDto>.Ok(MapPromotion(promotion));
    }

    public async Task<ServiceResult<PromotionSummaryDto>> CreateCompanyPromotionAsync(Guid companyId, PromotionUpsertDto dto)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<PromotionSummaryDto>.Fail("VALIDATION", "Company id is required.");
        }

        var validationErrors = ValidateUpsertDto(dto);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<PromotionSummaryDto>.Fail(validationErrors);
        }

        var result = await _companiesModuleApi.CreateCompanyPromotionAsync(companyId, MapUpsert(dto));
        return result.Success && result.Promotion != null
            ? ServiceResult<PromotionSummaryDto>.Ok(MapPromotion(result.Promotion))
            : ServiceResult<PromotionSummaryDto>.Fail(result.ErrorCode ?? "ERROR", result.ErrorMessage ?? "Failed to create promotion.");
    }

    public async Task<ServiceResult<PromotionSummaryDto>> UpdateCompanyPromotionAsync(Guid companyId, Guid promotionId, PromotionUpsertDto dto)
    {
        if (companyId == Guid.Empty || promotionId == Guid.Empty)
        {
            return ServiceResult<PromotionSummaryDto>.Fail("VALIDATION", "Company id and promotion id are required.");
        }

        var validationErrors = ValidateUpsertDto(dto);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<PromotionSummaryDto>.Fail(validationErrors);
        }

        var result = await _companiesModuleApi.UpdateCompanyPromotionAsync(companyId, promotionId, MapUpsert(dto));
        return result.Success && result.Promotion != null
            ? ServiceResult<PromotionSummaryDto>.Ok(MapPromotion(result.Promotion))
            : ServiceResult<PromotionSummaryDto>.Fail(result.ErrorCode ?? "ERROR", result.ErrorMessage ?? "Failed to update promotion.");
    }

    public async Task<ServiceResult> DeleteCompanyPromotionAsync(Guid companyId, Guid promotionId)
    {
        if (companyId == Guid.Empty || promotionId == Guid.Empty)
        {
            return ServiceResult.Fail("VALIDATION", "Company id and promotion id are required.");
        }

        var deleted = await _companiesModuleApi.DeleteCompanyPromotionAsync(companyId, promotionId);
        if (!deleted)
        {
            return ServiceResult.Fail("FORBIDDEN", "Promotion not found or access denied.");
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<List<UserPromotionDto>>> GetUserPromotionsAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<List<UserPromotionDto>>.Fail("VALIDATION", "User id is required.");
        }

        var entries = await _companiesModuleApi.GetUserPromotionsAsync(userId);
        return ServiceResult<List<UserPromotionDto>>.Ok(entries.Select(MapUserPromotion).ToList());
    }

    public async Task<ServiceResult<UserPromotionDto>> RedeemPromotionAsync(Guid userId, string code)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<UserPromotionDto>.Fail("VALIDATION", "User id is required.");
        }

        var normalizedCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ServiceResult<UserPromotionDto>.Fail("VALIDATION", "Promotion code is required.");
        }

        var result = await _companiesModuleApi.RedeemPromotionAsync(userId, normalizedCode);
        if (!result.Success || result.Promotion == null)
        {
            return ServiceResult<UserPromotionDto>.Fail(
                result.ErrorCode ?? "ERROR",
                result.ErrorMessage ?? "Failed to redeem promotion.");
        }

        var promotions = await _companiesModuleApi.GetUserPromotionsAsync(userId);
        var latest = promotions
            .Where(x => x.PromotionId == result.Promotion.Id)
            .OrderByDescending(x => x.AddedAtUtc)
            .FirstOrDefault();

        return latest == null
            ? ServiceResult<UserPromotionDto>.Fail("ERROR", "Promotion was redeemed but could not be loaded.")
            : ServiceResult<UserPromotionDto>.Ok(MapUserPromotion(latest));
    }

    public async Task<ServiceResult> RemoveUserPromotionAsync(Guid userId, Guid userPromotionId)
    {
        if (userId == Guid.Empty || userPromotionId == Guid.Empty)
        {
            return ServiceResult.Fail("VALIDATION", "User id and user promotion id are required.");
        }

        var removed = await _companiesModuleApi.RemoveUserPromotionAsync(userId, userPromotionId);
        if (!removed)
        {
            return ServiceResult.Fail("FORBIDDEN", "Promotion not found or access denied.");
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<AppliedPromotionDto>> ValidateUserPromotionAsync(Guid userId, Guid stationId, string code)
    {
        if (userId == Guid.Empty || stationId == Guid.Empty)
        {
            return ServiceResult<AppliedPromotionDto>.Fail("VALIDATION", "User id and station id are required.");
        }

        var normalizedCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ServiceResult<AppliedPromotionDto>.Fail("VALIDATION", "Promotion code is required.");
        }

        var station = await _chargingModuleApi.GetStationByIdAsync(stationId);
        if (station == null)
        {
            return ServiceResult<AppliedPromotionDto>.Fail("NOT_FOUND", "Charging station not found.");
        }

        return await ValidateUserPromotionForCompanyAsync(userId, station.CompanyId, code);
    }

    public async Task<ServiceResult<AppliedPromotionDto>> ValidateUserPromotionForCompanyAsync(Guid userId, Guid? companyId, string code)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<AppliedPromotionDto>.Fail("VALIDATION", "User id is required.");
        }

        var normalizedCode = NormalizeCode(code);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ServiceResult<AppliedPromotionDto>.Fail("VALIDATION", "Promotion code is required.");
        }

        var userPromotion = await _companiesModuleApi.GetValidUserPromotionByCodeAsync(userId, normalizedCode);

        if (userPromotion?.Promotion == null)
        {
            return ServiceResult<AppliedPromotionDto>.Fail("VALIDATION", "Promotion code is not available in your wallet.");
        }

        if (userPromotion.Promotion.CompanyId.HasValue
            && (!companyId.HasValue || userPromotion.Promotion.CompanyId.Value != companyId.Value))
        {
            return ServiceResult<AppliedPromotionDto>.Fail(
                "PROMOTION_COMPANY_MISMATCH",
                "This promotion cannot be used at this charging station because it belongs to another company.");
        }

        return ServiceResult<AppliedPromotionDto>.Ok(
            new AppliedPromotionDto
            {
                PromotionId = userPromotion.PromotionId,
                Code = userPromotion.Promotion.Code,
                DiscountValue = userPromotion.Promotion.DiscountValue
            });
    }

    private static List<ServiceError> ValidateUpsertDto(PromotionUpsertDto dto)
    {
        var errors = new List<ServiceError>();
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "Promotion code is required." });
        }

        if (dto.DiscountValue <= 0 || dto.DiscountValue >= 100)
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "Discount value must be between 0 and 100." });
        }

        var validFromUtc = ToUtc(dto.ValidFromUtc);
        var validToUtc = ToUtc(dto.ValidToUtc);
        if (validFromUtc >= validToUtc)
        {
            errors.Add(new ServiceError { Code = "VALIDATION", Message = "ValidFrom must be earlier than ValidTo." });
        }

        return errors;
    }

    private static PromotionSummaryDto MapPromotion(CompanyPromotionContract promotion)
    {
        return new PromotionSummaryDto
        {
            Id = promotion.Id,
            Code = promotion.Code,
            DiscountValue = promotion.DiscountValue,
            ValidFromUtc = promotion.ValidFromUtc,
            ValidToUtc = promotion.ValidToUtc,
            IsActive = promotion.IsActive
        };
    }

    private static UserPromotionDto MapUserPromotion(Shared.Contracts.Companies.UserPromotionContract userPromotion)
    {
        var promotion = userPromotion.Promotion;
        return new UserPromotionDto
        {
            Id = userPromotion.Id,
            PromotionId = userPromotion.PromotionId,
            Code = promotion?.Code ?? string.Empty,
            DiscountValue = promotion?.DiscountValue ?? 0,
            ValidFromUtc = promotion?.ValidFromUtc ?? DateTime.MinValue,
            ValidToUtc = promotion?.ValidToUtc ?? DateTime.MinValue,
            AddedAtUtc = userPromotion.AddedAtUtc,
            IsActive = promotion != null && promotion.IsActive && promotion.ValidFromUtc <= DateTime.UtcNow && promotion.ValidToUtc >= DateTime.UtcNow,
            IsUsed = userPromotion.IsUsed
        };
    }

    private static UpsertCompanyPromotionContract MapUpsert(PromotionUpsertDto dto) => new()
    {
        Code = dto.Code,
        DiscountValue = dto.DiscountValue,
        ValidFromUtc = dto.ValidFromUtc,
        ValidToUtc = dto.ValidToUtc,
        IsActive = dto.IsActive
    };

    private static string NormalizeCode(string code)
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
        };
    }
}
