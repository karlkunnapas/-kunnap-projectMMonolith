using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;

namespace App.BLL.Services;

public class ChargingSessionService : IChargingSessionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReservationService _reservationService;
    private readonly IPricingService _pricingService;
    private readonly IPromotionService _promotionService;

    public ChargingSessionService(
        IUnitOfWork unitOfWork,
        IReservationService reservationService,
        IPricingService pricingService,
        IPromotionService promotionService)
    {
        _unitOfWork = unitOfWork;
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

        var reservation = await _unitOfWork.Reservations.GetByIdForUserAsync(dto.ReservationId.Value, userId);
        if (reservation == null)
        {
            return ServiceResult<ChargingSessionDto>.Fail("FORBIDDEN", "Reservation not found or access denied.");
        }

        var existingSession = await _unitOfWork.ChargingSessions.GetByReservationIdAsync(reservation.Id);
        if (existingSession != null)
        {
            return ServiceResult<ChargingSessionDto>.Fail("VALIDATION", "Charging session has already been started for this reservation.");
        }

        if (reservation.Status != EReservationStatus.Started)
        {
            var startReservationResult = await _reservationService.StartReservationAsync(reservation.Id, userId);
            if (!startReservationResult.Success)
            {
                return ServiceResult<ChargingSessionDto>.Fail(startReservationResult.Errors);
            }

            reservation = await _unitOfWork.Reservations.GetByIdForUserAsync(reservation.Id, userId);
            if (reservation == null || reservation.Status != EReservationStatus.Started)
            {
                return ServiceResult<ChargingSessionDto>.Fail("VALIDATION", "Reservation is not in a started state.");
            }
        }

        var session = new ChargingSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = reservation.ChargingStationId,
            ReservationId = reservation.Id,
            PromotionId = reservation.PromotionId,
            StartTime = DateTime.UtcNow,
            EndTime = null,
            EnergyConsumed = 0m,
            Cost = 0m
        };

        await _unitOfWork.ChargingSessions.AddAsync(session);
        await _unitOfWork.SaveAsync();

        var persisted = await _unitOfWork.ChargingSessions.GetByIdForUserAsync(session.Id, userId);
        if (persisted == null)
        {
            return ServiceResult<ChargingSessionDto>.Fail("NOT_FOUND", "Charging session could not be loaded after creation.");
        }

        return ServiceResult<ChargingSessionDto>.Ok(MapSession(persisted));
    }

    public async Task<ServiceResult<ChargingSessionDto>> StopSessionAsync(Guid userId, Guid sessionId, ChargingSessionStopRequestDto dto)
    {
        var session = await _unitOfWork.ChargingSessions.GetByIdForUserAsync(sessionId, userId);
        if (session == null)
        {
            return ServiceResult<ChargingSessionDto>.Fail("FORBIDDEN", "Charging session not found or access denied.");
        }

        if (session.EndTime.HasValue)
        {
            return ServiceResult<ChargingSessionDto>.Fail("VALIDATION", "Charging session is already completed.");
        }

        var nowUtc = DateTime.UtcNow;
        var durationMinutes = Math.Max(1, (int)Math.Ceiling((nowUtc - session.StartTime).TotalMinutes));
        var energyConsumedKwh = CalculateEnergyEstimateKwh(durationMinutes, session.ChargingStation?.MaxPower);

        var costResult = await _pricingService.CalculateSessionFinalCostAsync(session.ChargingStationId, energyConsumedKwh, durationMinutes);
        if (!costResult.Success)
        {
            return ServiceResult<ChargingSessionDto>.Fail(costResult.Errors);
        }

        session.EndTime = nowUtc;
        session.EnergyConsumed = energyConsumedKwh;
        session.Cost = costResult.Data;

        AppliedPromotionDto? appliedPromotion = null;
        if (session.PromotionId.HasValue)
        {
            var userPromotion = await _unitOfWork.UserPromotions.GetByUserAndPromotionAsync(userId, session.PromotionId.Value);
            if (userPromotion == null || userPromotion.IsUsed)
            {
                return ServiceResult<ChargingSessionDto>.Fail("VALIDATION", "Promotion code is not available in your wallet.");
            }

            var lockedPromotion = session.Promotion ?? session.Reservation?.Promotion;
            if (lockedPromotion == null)
            {
                return ServiceResult<ChargingSessionDto>.Fail("VALIDATION", "Promotion code is not available in your wallet.");
            }

            appliedPromotion = new AppliedPromotionDto
            {
                PromotionId = lockedPromotion.Id,
                Code = lockedPromotion.Code,
                DiscountValue = lockedPromotion.DiscountValue
            };
        }
        else if (!string.IsNullOrWhiteSpace(dto.PromotionCode))
        {
            var promotionResult = await _promotionService.ValidateUserPromotionForCompanyAsync(
                userId,
                session.ChargingStation?.CompanyId,
                dto.PromotionCode);
            if (!promotionResult.Success || promotionResult.Data == null)
            {
                return ServiceResult<ChargingSessionDto>.Fail(promotionResult.Errors);
            }

            appliedPromotion = promotionResult.Data;
        }

        if (appliedPromotion != null)
        {
            session.Cost = ApplyDiscount(session.Cost, appliedPromotion.DiscountValue);
            session.PromotionId = appliedPromotion.PromotionId;

            var consumeResult = await ConsumeUserPromotionAsync(userId, appliedPromotion.PromotionId);
            if (!consumeResult.Success)
            {
                return ServiceResult<ChargingSessionDto>.Fail(consumeResult.Errors);
            }
        }

        if (session.ChargingStation != null)
        {
            session.ChargingStation.Status = EStationStatus.Available;
        }

        _unitOfWork.ChargingSessions.Update(session);
        await _unitOfWork.SaveAsync();

        return ServiceResult<ChargingSessionDto>.Ok(MapSession(session));
    }

    public async Task<ServiceResult<ChargingSessionDetailsDto>> GetSessionDetailsAsync(Guid sessionId, Guid userId)
    {
        var session = await _unitOfWork.ChargingSessions.GetByIdForUserAsync(sessionId, userId);
        if (session == null)
        {
            return ServiceResult<ChargingSessionDetailsDto>.Fail("FORBIDDEN", "Charging session not found or access denied.");
        }

        return ServiceResult<ChargingSessionDetailsDto>.Ok(MapSessionDetails(session));
    }

    public async Task<ServiceResult<List<ChargingSessionDto>>> GetUserSessionsAsync(Guid userId)
    {
        var sessions = await _unitOfWork.ChargingSessions.GetByUserIdAsync(userId);
        var dto = sessions.Select(MapSession).ToList();
        return ServiceResult<List<ChargingSessionDto>>.Ok(dto);
    }

    public async Task<ServiceResult<decimal>> CalculateFinalCostAsync(Guid sessionId)
    {
        var session = await _unitOfWork.ChargingSessions.GetByIdAsync(sessionId);
        if (session == null)
        {
            return ServiceResult<decimal>.Fail("NOT_FOUND", "Charging session not found.");
        }

        if (session.EndTime == null)
        {
            return ServiceResult<decimal>.Fail("VALIDATION", "Charging session is still active.");
        }

        return ServiceResult<decimal>.Ok(session.Cost);
    }

    private static ChargingSessionDto MapSession(ChargingSession session)
    {
        var promotionCode = session.Promotion?.Code ?? session.Reservation?.Promotion?.Code;
        var discountPercent = session.Promotion?.DiscountValue ?? session.Reservation?.Promotion?.DiscountValue ?? 0m;
        var baseCost = discountPercent > 0m ? RecoverBaseCost(session.Cost, discountPercent) : session.Cost;
        var discountAmount = Math.Max(0m, baseCost - session.Cost);

        return new ChargingSessionDto
        {
            Id = session.Id,
            StationId = session.ChargingStationId,
            StationName = session.ChargingStation?.Name.Translate() ?? session.ChargingStation?.Name.ToString() ?? string.Empty,
            ReservationId = session.ReservationId,
            StartTimeUtc = session.StartTime,
            EndTimeUtc = session.EndTime,
            EnergyConsumedKwh = session.EnergyConsumed,
            Cost = session.Cost,
            BaseCostBeforeDiscount = baseCost,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            PromotionCode = promotionCode,
            IsActive = session.EndTime == null
        };
    }

    private static ChargingSessionDetailsDto MapSessionDetails(ChargingSession session)
    {
        var durationMinutes = session.EndTime.HasValue
            ? Math.Max(1, (int)Math.Ceiling((session.EndTime.Value - session.StartTime).TotalMinutes))
            : Math.Max(1, (int)Math.Ceiling((DateTime.UtcNow - session.StartTime).TotalMinutes));

        var energyConsumedKwh = session.EndTime.HasValue
            ? session.EnergyConsumed
            : CalculateEnergyEstimateKwh(durationMinutes, session.ChargingStation?.MaxPower);

        var calculatedCost = session.EndTime.HasValue
            ? session.Cost
            : Math.Round((session.ChargingStation?.PricePerKwh ?? 0m) * energyConsumedKwh, 2, MidpointRounding.AwayFromZero);
        var promotionCode = session.Promotion?.Code ?? session.Reservation?.Promotion?.Code;
        var discountPercent = session.Promotion?.DiscountValue ?? session.Reservation?.Promotion?.DiscountValue ?? 0m;
        var baseCost = session.EndTime.HasValue && discountPercent > 0m
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
            StationName = session.ChargingStation?.Name.Translate() ?? session.ChargingStation?.Name.ToString() ?? string.Empty,
            ReservationId = session.ReservationId,
            StartTimeUtc = session.StartTime,
            EndTimeUtc = session.EndTime,
            DurationMinutes = durationMinutes,
            EnergyConsumedKwh = energyConsumedKwh,
            Cost = discountedCost,
            BaseCostBeforeDiscount = baseCost,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            PromotionCode = promotionCode,
            IsActive = session.EndTime == null
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

    private Task<ServiceResult> ConsumeUserPromotionAsync(Guid userId, Guid promotionId)
    {
        return ConsumeUserPromotionInternalAsync(userId, promotionId);
    }

    private async Task<ServiceResult> ConsumeUserPromotionInternalAsync(Guid userId, Guid promotionId)
    {
        var userPromotion = await _unitOfWork.UserPromotions.GetByUserAndPromotionAsync(userId, promotionId);
        if (userPromotion == null)
        {
            return ServiceResult.Fail("VALIDATION", "Promotion code is not available in your wallet.");
        }

        if (userPromotion.IsUsed)
        {
            return ServiceResult.Fail("VALIDATION", "Promotion code is not available in your wallet.");
        }

        userPromotion.IsUsed = true;
        return ServiceResult.Ok();
    }

    private static decimal RecoverBaseCost(decimal discountedCost, decimal discountPercent)
    {
        var safeDiscount = Math.Min(100m, Math.Max(0m, discountPercent));
        if (safeDiscount <= 0m)
        {
            return discountedCost;
        }

        if (safeDiscount >= 100m)
        {
            return discountedCost;
        }

        var baseCost = discountedCost / (1m - safeDiscount / 100m);
        return Math.Round(Math.Max(0m, baseCost), 2, MidpointRounding.AwayFromZero);
    }
}
