using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using Shared.Contracts.Charging;

namespace App.BLL.Services;

public class ChargingSessionService : IChargingSessionService
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly IReservationService _reservationService;
    private readonly IPricingService _pricingService;
    private readonly IPromotionService _promotionService;

    public ChargingSessionService(
        IChargingModuleApi chargingModuleApi,
        IReservationService reservationService,
        IPricingService pricingService,
        IPromotionService promotionService)
    {
        _chargingModuleApi = chargingModuleApi;
        _reservationService = reservationService;
        _pricingService = pricingService;
        _promotionService = promotionService;
    }

    public async Task<ServiceResult<ChargingSessionDto>> StartSessionAsync(Guid userId, ChargingSessionStartRequestDto dto)
    {
        if (dto.ReservationId == null || dto.ReservationId == Guid.Empty)
        {
            return ServiceResult<ChargingSessionDto>.Fail("VALIDATION", "Reservation is required to start a charging session.");
        }

        var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(dto.ReservationId.Value, userId);
        if (reservation == null)
        {
            return ServiceResult<ChargingSessionDto>.Fail("FORBIDDEN", "Reservation not found or access denied.");
        }

        var existingSession = await _chargingModuleApi.GetChargingSessionByReservationIdAsync(reservation.Id);
        if (existingSession != null)
        {
            return ServiceResult<ChargingSessionDto>.Fail("VALIDATION", "Charging session has already been started for this reservation.");
        }

        if (reservation.Status != Shared.Contracts.Charging.EReservationStatus.Started)
        {
            var startReservationResult = await _reservationService.StartReservationAsync(reservation.Id, userId);
            if (!startReservationResult.Success)
            {
                return ServiceResult<ChargingSessionDto>.Fail(startReservationResult.Errors);
            }

            reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(reservation.Id, userId);
            if (reservation == null || reservation.Status != Shared.Contracts.Charging.EReservationStatus.Started)
            {
                return ServiceResult<ChargingSessionDto>.Fail("VALIDATION", "Reservation is not in a started state.");
            }
        }

        var sessionContract = new ChargingSessionContract
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = reservation.ChargingStationId,
            ReservationId = reservation.Id,
            PromotionId = reservation.PromotionId,
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = null,
            EnergyConsumed = 0m,
            Cost = 0m
        };

        var persisted = await _chargingModuleApi.CreateChargingSessionAsync(sessionContract);
        await EnrichSessionPromotionAsync(userId, persisted);
        return ServiceResult<ChargingSessionDto>.Ok(MapSession(persisted));
    }

    public async Task<ServiceResult<ChargingSessionDto>> StopSessionAsync(Guid userId, Guid sessionId, ChargingSessionStopRequestDto dto)
    {
        var session = await _chargingModuleApi.GetChargingSessionByIdForUserAsync(sessionId, userId);
        if (session == null)
        {
            return ServiceResult<ChargingSessionDto>.Fail("FORBIDDEN", "Charging session not found or access denied.");
        }

        if (session.EndTimeUtc.HasValue)
        {
            return ServiceResult<ChargingSessionDto>.Fail("VALIDATION", "Charging session is already completed.");
        }

        var nowUtc = DateTime.UtcNow;
        var durationMinutes = Math.Max(1, (int)Math.Ceiling((nowUtc - session.StartTimeUtc).TotalMinutes));
        var energyConsumedKwh = CalculateEnergyEstimateKwh(durationMinutes, session.StationMaxPower);

        var costResult = await _pricingService.CalculateSessionFinalCostAsync(session.ChargingStationId, energyConsumedKwh, durationMinutes);
        if (!costResult.Success)
        {
            return ServiceResult<ChargingSessionDto>.Fail(costResult.Errors);
        }

        var finalCost = costResult.Data;
        Guid? finalPromotionId = session.PromotionId;
        AppliedPromotionDto? appliedPromotion = null;

        if (session.PromotionId.HasValue)
        {
            var userPromotionsResult = await _promotionService.GetUserPromotionsAsync(userId);
            if (!userPromotionsResult.Success || userPromotionsResult.Data == null)
            {
                return ServiceResult<ChargingSessionDto>.Fail(userPromotionsResult.Errors);
            }

            var lockedPromotion = userPromotionsResult.Data
                .FirstOrDefault(p => p.PromotionId == session.PromotionId.Value && !p.IsUsed);
            if (lockedPromotion == null)
            {
                return ServiceResult<ChargingSessionDto>.Fail("VALIDATION", "Promotion code is not available in your wallet.");
            }

            appliedPromotion = new AppliedPromotionDto
            {
                PromotionId = lockedPromotion.PromotionId,
                Code = lockedPromotion.Code,
                DiscountValue = lockedPromotion.DiscountValue
            };
        }
        else if (!string.IsNullOrWhiteSpace(dto.PromotionCode))
        {
            var station = await _chargingModuleApi.GetStationByIdAsync(session.ChargingStationId);
            var promotionResult = await _promotionService.ValidateUserPromotionForCompanyAsync(
                userId,
                station?.CompanyId,
                dto.PromotionCode);
            if (!promotionResult.Success || promotionResult.Data == null)
            {
                return ServiceResult<ChargingSessionDto>.Fail(promotionResult.Errors);
            }

            appliedPromotion = promotionResult.Data;
        }

        if (appliedPromotion != null)
        {
            finalCost = ApplyDiscount(finalCost, appliedPromotion.DiscountValue);
            finalPromotionId = appliedPromotion.PromotionId;

            var consumeResult = await ConsumeUserPromotionAsync(userId, appliedPromotion.PromotionId);
            if (!consumeResult.Success)
            {
                return ServiceResult<ChargingSessionDto>.Fail(consumeResult.Errors);
            }
        }

        var completed = await _chargingModuleApi.CompleteChargingSessionAsync(
            session.Id,
            endTimeUtc: nowUtc,
            energyConsumed: energyConsumedKwh,
            cost: finalCost,
            promotionId: finalPromotionId,
            stationStatus: Shared.Contracts.Charging.EStationStatus.Available);

        if (!completed)
        {
            return ServiceResult<ChargingSessionDto>.Fail("NOT_FOUND", "Charging session not found.");
        }

        var updated = await _chargingModuleApi.GetChargingSessionByIdForUserAsync(session.Id, userId);
        if (updated != null)
        {
            await EnrichSessionPromotionAsync(userId, updated);
        }
        return updated == null
            ? ServiceResult<ChargingSessionDto>.Fail("NOT_FOUND", "Charging session not found.")
            : ServiceResult<ChargingSessionDto>.Ok(MapSession(updated));
    }

    public async Task<ServiceResult<ChargingSessionDetailsDto>> GetSessionDetailsAsync(Guid sessionId, Guid userId)
    {
        var session = await _chargingModuleApi.GetChargingSessionByIdForUserAsync(sessionId, userId);
        if (session == null)
        {
            return ServiceResult<ChargingSessionDetailsDto>.Fail("FORBIDDEN", "Charging session not found or access denied.");
        }

        await EnrichSessionPromotionAsync(userId, session);
        return ServiceResult<ChargingSessionDetailsDto>.Ok(MapSessionDetails(session));
    }

    public async Task<ServiceResult<List<ChargingSessionDto>>> GetUserSessionsAsync(Guid userId)
    {
        var sessions = (await _chargingModuleApi.GetUserChargingSessionsAsync(userId)).ToList();
        await EnrichSessionsPromotionAsync(userId, sessions);
        return ServiceResult<List<ChargingSessionDto>>.Ok(sessions.Select(MapSession).ToList());
    }

    public async Task<ServiceResult<decimal>> CalculateFinalCostAsync(Guid sessionId)
    {
        var session = await _chargingModuleApi.GetChargingSessionByIdAsync(sessionId);
        if (session == null)
        {
            return ServiceResult<decimal>.Fail("NOT_FOUND", "Charging session not found.");
        }

        if (session.EndTimeUtc == null)
        {
            return ServiceResult<decimal>.Fail("VALIDATION", "Charging session is still active.");
        }

        return ServiceResult<decimal>.Ok(session.Cost);
    }

    private static ChargingSessionDto MapSession(ChargingSessionContract session)
    {
        var discountPercent = session.PromotionDiscountValue ?? 0m;
        var baseCost = discountPercent > 0m ? RecoverBaseCost(session.Cost, discountPercent) : session.Cost;
        var discountAmount = Math.Max(0m, baseCost - session.Cost);

        return new ChargingSessionDto
        {
            Id = session.Id,
            StationId = session.ChargingStationId,
            StationName = session.StationName,
            ReservationId = session.ReservationId,
            StartTimeUtc = session.StartTimeUtc,
            EndTimeUtc = session.EndTimeUtc,
            EnergyConsumedKwh = session.EnergyConsumed,
            Cost = session.Cost,
            BaseCostBeforeDiscount = baseCost,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            PromotionCode = session.PromotionCode,
            IsActive = session.EndTimeUtc == null
        };
    }

    private static ChargingSessionDetailsDto MapSessionDetails(ChargingSessionContract session)
    {
        var durationMinutes = session.EndTimeUtc.HasValue
            ? Math.Max(1, (int)Math.Ceiling((session.EndTimeUtc.Value - session.StartTimeUtc).TotalMinutes))
            : Math.Max(1, (int)Math.Ceiling((DateTime.UtcNow - session.StartTimeUtc).TotalMinutes));

        var energyConsumedKwh = session.EndTimeUtc.HasValue
            ? session.EnergyConsumed
            : CalculateEnergyEstimateKwh(durationMinutes, session.StationMaxPower);

        var calculatedCost = session.EndTimeUtc.HasValue
            ? session.Cost
            : Math.Round(session.StationPricePerKwh * energyConsumedKwh, 2, MidpointRounding.AwayFromZero);

        var discountPercent = session.PromotionDiscountValue ?? 0m;
        var baseCost = session.EndTimeUtc.HasValue && discountPercent > 0m
            ? RecoverBaseCost(calculatedCost, discountPercent)
            : calculatedCost;
        var discountedCost = discountPercent > 0m
            ? ApplyDiscount(baseCost, discountPercent)
            : calculatedCost;
        var discountAmount = Math.Max(0m, baseCost - discountedCost);

        return new ChargingSessionDetailsDto
        {
            Id = session.Id,
            StationId = session.ChargingStationId,
            StationName = session.StationName,
            ReservationId = session.ReservationId,
            StartTimeUtc = session.StartTimeUtc,
            EndTimeUtc = session.EndTimeUtc,
            DurationMinutes = durationMinutes,
            EnergyConsumedKwh = energyConsumedKwh,
            Cost = discountedCost,
            BaseCostBeforeDiscount = baseCost,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            PromotionCode = session.PromotionCode,
            IsActive = session.EndTimeUtc == null
        };
    }

    private static decimal CalculateEnergyEstimateKwh(int durationMinutes, decimal? stationMaxPower)
    {
        var effectivePower = Math.Max(1m, Math.Min(stationMaxPower ?? 50m, 200m));
        var durationHours = durationMinutes / 60m;
        return Math.Round(durationHours * effectivePower, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ApplyDiscount(decimal baseCost, decimal discountValue)
    {
        var safeDiscount = Math.Min(100m, Math.Max(0m, discountValue));
        var discounted = baseCost * (1m - safeDiscount / 100m);
        return Math.Round(Math.Max(0m, discounted), 2, MidpointRounding.AwayFromZero);
    }

    private async Task<ServiceResult> ConsumeUserPromotionAsync(Guid userId, Guid promotionId)
    {
        var userPromotionsResult = await _promotionService.GetUserPromotionsAsync(userId);
        if (!userPromotionsResult.Success || userPromotionsResult.Data == null)
        {
            return ServiceResult.Fail(userPromotionsResult.Errors.FirstOrDefault()?.Code ?? "ERROR", userPromotionsResult.Errors.FirstOrDefault()?.Message ?? "Unable to load user promotions.");
        }

        var userPromotion = userPromotionsResult.Data
            .FirstOrDefault(x => x.PromotionId == promotionId && !x.IsUsed);
        if (userPromotion == null)
        {
            return ServiceResult.Fail("VALIDATION", "Promotion code is not available in your wallet.");
        }

        var removeResult = await _promotionService.RemoveUserPromotionAsync(userId, userPromotion.Id);
        if (!removeResult.Success)
        {
            return ServiceResult.Fail(removeResult.Errors);
        }

        return ServiceResult.Ok();
    }

    private async Task EnrichSessionsPromotionAsync(Guid userId, List<ChargingSessionContract> sessions)
    {
        var promotionIds = sessions
            .Where(s => s.PromotionId.HasValue)
            .Select(s => s.PromotionId!.Value)
            .Distinct()
            .ToList();
        if (promotionIds.Count == 0)
        {
            return;
        }

        var promotionsResult = await _promotionService.GetUserPromotionsAsync(userId);
        if (!promotionsResult.Success || promotionsResult.Data == null)
        {
            return;
        }

        var byId = promotionsResult.Data.ToDictionary(x => x.PromotionId, x => x);
        foreach (var session in sessions.Where(s => s.PromotionId.HasValue))
        {
            if (!byId.TryGetValue(session.PromotionId!.Value, out var promotion))
            {
                continue;
            }

            session.PromotionCode = promotion.Code;
            session.PromotionDiscountValue = promotion.DiscountValue;
        }
    }

    private async Task EnrichSessionPromotionAsync(Guid userId, ChargingSessionContract session)
    {
        if (!session.PromotionId.HasValue)
        {
            return;
        }

        var promotionsResult = await _promotionService.GetUserPromotionsAsync(userId);
        if (!promotionsResult.Success || promotionsResult.Data == null)
        {
            return;
        }

        var promotion = promotionsResult.Data.FirstOrDefault(x => x.PromotionId == session.PromotionId.Value);
        if (promotion == null)
        {
            return;
        }

        session.PromotionCode = promotion.Code;
        session.PromotionDiscountValue = promotion.DiscountValue;
    }

    private static decimal RecoverBaseCost(decimal discountedCost, decimal discountPercent)
    {
        var safeDiscount = Math.Min(100m, Math.Max(0m, discountPercent));
        if (safeDiscount <= 0m || safeDiscount >= 100m)
        {
            return discountedCost;
        }

        var baseCost = discountedCost / (1m - safeDiscount / 100m);
        return Math.Round(Math.Max(0m, baseCost), 2, MidpointRounding.AwayFromZero);
    }
}
