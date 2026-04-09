using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;

namespace App.BLL.Services;

public class PromotionService : IPromotionService
{
    private readonly IUnitOfWork _unitOfWork;

    public PromotionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<List<PromotionSummaryDto>>> GetCompanyPromotionsAsync(Guid companyId)
    {
        if (companyId == Guid.Empty)
        {
            return ServiceResult<List<PromotionSummaryDto>>.Fail("VALIDATION", "Company id is required.");
        }

        var promotions = await _unitOfWork.Promotions.GetByCompanyAsync(companyId);
        return ServiceResult<List<PromotionSummaryDto>>.Ok(promotions.Select(MapPromotion).ToList());
    }

    public async Task<ServiceResult<PromotionSummaryDto>> GetCompanyPromotionAsync(Guid companyId, Guid promotionId)
    {
        if (companyId == Guid.Empty || promotionId == Guid.Empty)
        {
            return ServiceResult<PromotionSummaryDto>.Fail("VALIDATION", "Company id and promotion id are required.");
        }

        var promotion = await _unitOfWork.Promotions.GetByIdForCompanyAsync(promotionId, companyId);
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

        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Code = NormalizeCode(dto.Code),
            DiscountValue = dto.DiscountValue,
            ValidFrom = ToUtc(dto.ValidFromUtc),
            ValidTo = ToUtc(dto.ValidToUtc),
            IsActive = dto.IsActive
        };

        await _unitOfWork.Promotions.AddAsync(promotion);
        await _unitOfWork.SaveAsync();

        return ServiceResult<PromotionSummaryDto>.Ok(MapPromotion(promotion));
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

        var promotion = await _unitOfWork.Promotions.GetByIdForCompanyAsync(promotionId, companyId);
        if (promotion == null)
        {
            return ServiceResult<PromotionSummaryDto>.Fail("FORBIDDEN", "Promotion not found or access denied.");
        }

        promotion.Code = NormalizeCode(dto.Code);
        promotion.DiscountValue = dto.DiscountValue;
        promotion.ValidFrom = ToUtc(dto.ValidFromUtc);
        promotion.ValidTo = ToUtc(dto.ValidToUtc);
        promotion.IsActive = dto.IsActive;

        _unitOfWork.Promotions.Update(promotion);
        await _unitOfWork.SaveAsync();

        return ServiceResult<PromotionSummaryDto>.Ok(MapPromotion(promotion));
    }

    public async Task<ServiceResult> DeleteCompanyPromotionAsync(Guid companyId, Guid promotionId)
    {
        if (companyId == Guid.Empty || promotionId == Guid.Empty)
        {
            return ServiceResult.Fail("VALIDATION", "Company id and promotion id are required.");
        }

        var promotion = await _unitOfWork.Promotions.GetByIdForCompanyAsync(promotionId, companyId);
        if (promotion == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Promotion not found or access denied.");
        }

        _unitOfWork.Promotions.Remove(promotion);
        await _unitOfWork.SaveAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<List<UserPromotionDto>>> GetUserPromotionsAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return ServiceResult<List<UserPromotionDto>>.Fail("VALIDATION", "User id is required.");
        }

        var entries = await _unitOfWork.UserPromotions.GetByUserIdAsync(userId);
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

        var candidates = await _unitOfWork.Promotions.GetActiveByCodeAsync(normalizedCode, DateTime.UtcNow);
        if (candidates.Count == 0)
        {
            return ServiceResult<UserPromotionDto>.Fail("NOT_FOUND", "Promotion code is invalid or expired.");
        }

        if (candidates.Count > 1)
        {
            return ServiceResult<UserPromotionDto>.Fail("VALIDATION", "Promotion code is ambiguous. Contact support.");
        }

        var promotion = candidates[0];
        var existing = await _unitOfWork.UserPromotions.GetByUserAndPromotionAsync(userId, promotion.Id);
        if (existing != null)
        {
            return ServiceResult<UserPromotionDto>.Fail("VALIDATION", "Promotion is already in your wallet.");
        }

        var userPromotion = new UserPromotion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PromotionId = promotion.Id,
            AddedAt = DateTime.UtcNow,
            IsUsed = false
        };

        await _unitOfWork.UserPromotions.AddAsync(userPromotion);
        await _unitOfWork.SaveAsync();

        userPromotion.Promotion = promotion;
        return ServiceResult<UserPromotionDto>.Ok(MapUserPromotion(userPromotion));
    }

    public async Task<ServiceResult> RemoveUserPromotionAsync(Guid userId, Guid userPromotionId)
    {
        if (userId == Guid.Empty || userPromotionId == Guid.Empty)
        {
            return ServiceResult.Fail("VALIDATION", "User id and user promotion id are required.");
        }

        var userPromotion = await _unitOfWork.UserPromotions.GetByIdForUserAsync(userPromotionId, userId);
        if (userPromotion == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Promotion not found or access denied.");
        }

        _unitOfWork.UserPromotions.Remove(userPromotion);
        await _unitOfWork.SaveAsync();

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

        var station = await _unitOfWork.ChargingStations.GetByIdWithDetailsAsync(stationId);
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

        var userPromotion = await _unitOfWork.UserPromotions.GetValidByCodeForUserAsync(
            userId,
            normalizedCode,
            DateTime.UtcNow);

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

        return ServiceResult<AppliedPromotionDto>.Ok(new AppliedPromotionDto
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

    private static PromotionSummaryDto MapPromotion(Promotion promotion)
    {
        return new PromotionSummaryDto
        {
            Id = promotion.Id,
            Code = promotion.Code,
            DiscountValue = promotion.DiscountValue,
            ValidFromUtc = promotion.ValidFrom,
            ValidToUtc = promotion.ValidTo,
            IsActive = promotion.IsActive
        };
    }

    private static UserPromotionDto MapUserPromotion(UserPromotion userPromotion)
    {
        return new UserPromotionDto
        {
            Id = userPromotion.Id,
            PromotionId = userPromotion.PromotionId,
            Code = userPromotion.Promotion?.Code ?? string.Empty,
            DiscountValue = userPromotion.Promotion?.DiscountValue ?? 0m,
            ValidFromUtc = userPromotion.Promotion?.ValidFrom ?? DateTime.UtcNow,
            ValidToUtc = userPromotion.Promotion?.ValidTo ?? DateTime.UtcNow,
            AddedAtUtc = userPromotion.AddedAt,
            IsActive = userPromotion.Promotion?.IsActive ?? false,
            IsUsed = userPromotion.IsUsed
        };
    }

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
