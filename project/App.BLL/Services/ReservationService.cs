using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using Shared.Contracts.Charging;

namespace App.BLL.Services;

public class ReservationService : IReservationService
{
    private readonly IChargingModuleApi _chargingModuleApi;
    private readonly IAvailabilityService _availabilityService;
    private readonly IPricingService _pricingService;
    private readonly IPromotionService _promotionService;

    public ReservationService(
        IChargingModuleApi chargingModuleApi,
        IAvailabilityService availabilityService,
        IPricingService pricingService,
        IPromotionService promotionService)
    {
        _chargingModuleApi = chargingModuleApi;
        _availabilityService = availabilityService;
        _pricingService = pricingService;
        _promotionService = promotionService;
    }

    public async Task<ServiceResult<StationDetailsDto>> GetStationDetailsAsync(Guid stationId, DateTime? dateUtc = null)
    {
        var station = await _chargingModuleApi.GetStationByIdAsync(stationId);
        if (station == null)
        {
            return ServiceResult<StationDetailsDto>.Fail("NOT_FOUND", "Charging station not found.");
        }

        var reservations = await _chargingModuleApi.GetStationReservationsAsync(stationId);
        var nowUtc = DateTime.UtcNow;
        var activeReservations = reservations.Where(r => IsBlockingReservation(r, nowUtc)).ToList();

        var connectorNames = station.Connectors
            .Where(c => c.IsActive)
            .Select(c => c.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        var connectorDetails = connectorNames
            .GroupBy(name => name)
            .Select(group => BllDtoFactory.CreateConnectorDetailDto(
                name: group.Key,
                quantity: group.Count(),
                availableQuantity: Math.Max(0, group.Count() - activeReservations.Count),
                reservations: activeReservations
                    .Select(r => BllDtoFactory.CreateReservedTimeRangeDto(r.StartTimeUtc, r.EndTimeUtc))
                    .OrderBy(r => r.StartTimeUtc)
                    .ToList()))
            .ToList();

        var stationReservations = reservations
            .Where(IsUpcomingCustomerReservation)
            .OrderBy(r => r.StartTimeUtc)
            .Select(r => BllDtoFactory.CreateStationReservationDto(r.StartTimeUtc, r.EndTimeUtc, GetEffectiveStatus(r)))
            .ToList();

        var dto = new StationDetailsDto
        {
            Id = station.Id,
            CompanyId = station.CompanyId,
            Name = station.Name,
            Location = station.Location,
            Status = MapStationStatus(station.Status),
            PricePerKwh = station.PricePerKwh,
            MaxPower = station.MaxPower,
            Connectors = connectorDetails,
            ExistingReservations = stationReservations,
            AvailableSlots = new List<AvailabilitySlotDto>()
        };

        return ServiceResult<StationDetailsDto>.Ok(dto);
    }

    public async Task<ServiceResult<ReservationDto>> ReserveAsync(Guid userId, ReservationCreateDto dto)
    {
        var station = await _chargingModuleApi.GetStationByIdAsync(dto.StationId);
        if (station == null)
        {
            return ServiceResult<ReservationDto>.Fail("NOT_FOUND", "Charging station not found.");
        }

        if (station.Status == Shared.Contracts.Charging.EStationStatus.Maintenance)
        {
            return ServiceResult<ReservationDto>.Fail("STATION_UNAVAILABLE", "Station is currently unavailable for reservations.");
        }

        var startUtc = ToUtc(dto.StartTimeUtc);
        var endUtc = ToUtc(dto.EndTimeUtc);

        if (startUtc < DateTime.UtcNow)
        {
            return ServiceResult<ReservationDto>.Fail("VALIDATION", "Start time must be in the future.");
        }

        var overlapResult = await _availabilityService.ValidateOverlapAsync(dto.StationId, startUtc, endUtc);
        if (!overlapResult.Success)
        {
            return ServiceResult<ReservationDto>.Fail(overlapResult.Errors);
        }

        var durationMinutes = (int)Math.Ceiling((endUtc - startUtc).TotalMinutes);
        var estimateResult = await _pricingService.CalculateReservationEstimateAsync(dto.StationId, durationMinutes, dto.EstimatedEnergyKwh);
        if (!estimateResult.Success || estimateResult.Data == null)
        {
            return ServiceResult<ReservationDto>.Fail(estimateResult.Errors);
        }

        var reservation = new ReservationContract
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = dto.StationId,
            StartTimeUtc = startUtc,
            EndTimeUtc = endUtc,
            ExpiresAtUtc = startUtc.AddMinutes(15),
            EstimatedCost = estimateResult.Data.EstimatedCost,
            Status = Shared.Contracts.Charging.EReservationStatus.Active
        };

        if (!string.IsNullOrWhiteSpace(dto.PromotionCode))
        {
            var promotionResult = await _promotionService.ValidateUserPromotionForCompanyAsync(userId, station.CompanyId, dto.PromotionCode);
            if (!promotionResult.Success || promotionResult.Data == null)
            {
                return ServiceResult<ReservationDto>.Fail(promotionResult.Errors);
            }

            reservation.EstimatedCost = ApplyDiscount(reservation.EstimatedCost, promotionResult.Data.DiscountValue);
            reservation.PromotionId = promotionResult.Data.PromotionId;
        }

        var created = await _chargingModuleApi.CreateReservationAsync(reservation);
        return ServiceResult<ReservationDto>.Ok(MapReservation(created));
    }

    public async Task<ServiceResult<List<ReservationDto>>> GetUserReservationsAsync(Guid userId)
    {
        var reservations = await _chargingModuleApi.GetUserReservationsAsync(userId);
        var filtered = reservations
            .Where(IsUpcomingCustomerReservation)
            .OrderBy(r => r.StartTimeUtc)
            .ToList();

        return ServiceResult<List<ReservationDto>>.Ok(filtered.Select(MapReservation).ToList());
    }

    public async Task<ServiceResult<ReservationDto>> GetReservationDetailsAsync(Guid reservationId, Guid userId)
    {
        var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(reservationId, userId);
        return reservation == null
            ? ServiceResult<ReservationDto>.Fail("FORBIDDEN", "Reservation not found or access denied.")
            : ServiceResult<ReservationDto>.Ok(MapReservation(reservation));
    }

    public async Task<ServiceResult> StartReservationAsync(Guid reservationId, Guid userId)
    {
        var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(reservationId, userId);
        if (reservation == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Reservation not found or access denied.");
        }

        var effectiveStatus = GetEffectiveStatus(reservation);
        if (effectiveStatus is App.Domain.EReservationStatus.Cancelled or App.Domain.EReservationStatus.Expired)
        {
            return ServiceResult.Fail("VALIDATION", "Reservation is already closed.");
        }

        if (reservation.Status == Shared.Contracts.Charging.EReservationStatus.Started)
        {
            return ServiceResult.Fail("VALIDATION", "Reservation is already started.");
        }

        var nowUtc = DateTime.UtcNow;
        if (nowUtc < reservation.StartTimeUtc)
        {
            return ServiceResult.Fail("VALIDATION", "Reservation can be started only from its start time.");
        }

        if (nowUtc >= reservation.EndTimeUtc)
        {
            return ServiceResult.Fail("VALIDATION", "Reservation has already ended.");
        }

        var updated = await _chargingModuleApi.UpdateReservationStatusAsync(
            reservation.Id,
            Shared.Contracts.Charging.EReservationStatus.Started,
            expiresAtUtc: reservation.EndTimeUtc,
            stationStatus: Shared.Contracts.Charging.EStationStatus.InUse);

        return updated ? ServiceResult.Ok() : ServiceResult.Fail("NOT_FOUND", "Reservation not found.");
    }

    public async Task<ServiceResult> CancelReservationAsync(Guid reservationId, Guid userId)
    {
        var reservation = await _chargingModuleApi.GetReservationByIdForUserAsync(reservationId, userId);
        if (reservation == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Reservation not found or access denied.");
        }

        var effectiveStatus = GetEffectiveStatus(reservation);
        if (effectiveStatus is App.Domain.EReservationStatus.Cancelled or App.Domain.EReservationStatus.Expired or App.Domain.EReservationStatus.Started)
        {
            return ServiceResult.Fail("VALIDATION", "Reservation is already closed.");
        }

        var updated = await _chargingModuleApi.UpdateReservationStatusAsync(
            reservation.Id,
            Shared.Contracts.Charging.EReservationStatus.Cancelled,
            cancelledAtUtc: DateTime.UtcNow);

        return updated ? ServiceResult.Ok() : ServiceResult.Fail("NOT_FOUND", "Reservation not found.");
    }

    public Task<ServiceResult<CostEstimateDto>> EstimateCostAsync(Guid stationId, int durationMinutes, decimal? estimatedKwh = null)
    {
        return _pricingService.CalculateReservationEstimateAsync(stationId, durationMinutes, estimatedKwh);
    }

    private static ReservationDto MapReservation(ReservationContract reservation)
    {
        return new ReservationDto
        {
            Id = reservation.Id,
            StationId = reservation.ChargingStationId,
            StationName = reservation.StationName,
            StartTimeUtc = reservation.StartTimeUtc,
            EndTimeUtc = reservation.EndTimeUtc,
            ExpiresAtUtc = reservation.ExpiresAtUtc,
            CancelledAtUtc = reservation.CancelledAtUtc,
            EstimatedCost = reservation.EstimatedCost,
            Status = GetEffectiveStatus(reservation)
        };
    }

    private static App.Domain.EReservationStatus GetEffectiveStatus(ReservationContract reservation)
    {
        if (reservation.Status == Shared.Contracts.Charging.EReservationStatus.Active && DateTime.UtcNow > reservation.ExpiresAtUtc)
        {
            return App.Domain.EReservationStatus.Expired;
        }

        return reservation.Status switch
        {
            Shared.Contracts.Charging.EReservationStatus.Active => App.Domain.EReservationStatus.Active,
            Shared.Contracts.Charging.EReservationStatus.Cancelled => App.Domain.EReservationStatus.Cancelled,
            Shared.Contracts.Charging.EReservationStatus.Expired => App.Domain.EReservationStatus.Expired,
            Shared.Contracts.Charging.EReservationStatus.Started => App.Domain.EReservationStatus.Started,
            _ => App.Domain.EReservationStatus.Active
        };
    }

    private static bool IsUpcomingCustomerReservation(ReservationContract reservation)
    {
        var effectiveStatus = GetEffectiveStatus(reservation);
        if (effectiveStatus is not App.Domain.EReservationStatus.Active and not App.Domain.EReservationStatus.Started)
        {
            return false;
        }

        return reservation.EndTimeUtc >= DateTime.UtcNow;
    }

    private static bool IsBlockingReservation(ReservationContract reservation, DateTime nowUtc)
    {
        var effectiveStatus = GetEffectiveStatus(reservation);
        if (effectiveStatus == App.Domain.EReservationStatus.Cancelled || effectiveStatus == App.Domain.EReservationStatus.Expired)
        {
            return false;
        }

        return reservation.StartTimeUtc <= nowUtc && reservation.EndTimeUtc > nowUtc;
    }

    private static App.Domain.EStationStatus MapStationStatus(Shared.Contracts.Charging.EStationStatus status)
    {
        return status switch
        {
            Shared.Contracts.Charging.EStationStatus.Available => App.Domain.EStationStatus.Available,
            Shared.Contracts.Charging.EStationStatus.InUse => App.Domain.EStationStatus.InUse,
            Shared.Contracts.Charging.EStationStatus.Maintenance => App.Domain.EStationStatus.Maintenance,
            _ => App.Domain.EStationStatus.Available
        };
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

    private static decimal ApplyDiscount(decimal baseCost, decimal discountPercent)
    {
        var safeDiscount = Math.Min(100m, Math.Max(0m, discountPercent));
        var discounted = baseCost * (1m - safeDiscount / 100m);
        return Math.Round(Math.Max(0m, discounted), 2, MidpointRounding.AwayFromZero);
    }
}
