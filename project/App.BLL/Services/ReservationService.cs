using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;
using App.Domain;

namespace App.BLL.Services;

public class ReservationService : IReservationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAvailabilityService _availabilityService;
    private readonly IPricingService _pricingService;
    private readonly IPromotionService _promotionService;

    public ReservationService(
        IUnitOfWork unitOfWork,
        IAvailabilityService availabilityService,
        IPricingService pricingService,
        IPromotionService promotionService)
    {
        _unitOfWork = unitOfWork;
        _availabilityService = availabilityService;
        _pricingService = pricingService;
        _promotionService = promotionService;
    }

    public async Task<ServiceResult<StationDetailsDto>> GetStationDetailsAsync(Guid stationId, DateTime? dateUtc = null)
    {
        var station = await _unitOfWork.ChargingStations.GetByIdWithDetailsAsync(stationId);
        if (station == null)
        {
            return ServiceResult<StationDetailsDto>.Fail("NOT_FOUND", "Charging station not found.");
        }

        var nowUtc = DateTime.UtcNow;
        var activeReservations = station.Reservations?
            .Where(r => IsBlockingReservation(r, nowUtc))
            .ToList() ?? new List<Reservation>();

        var connectorNames = station.ChargingStationConnectors?
            .Where(link => link.Connector != null && link.Connector.IsActive)
            .Select(link => link.Connector!.Name.Translate() ?? link.Connector.Name.ToString() ?? string.Empty)
            .ToList() ?? new List<string>();

        var connectorDetails = connectorNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(name => name)
            .Select(group => new ConnectorDetailDto
            {
                Name = group.Key,
                Quantity = group.Count(),
                AvailableQuantity = Math.Max(0, group.Count() - activeReservations.Count),
                Reservations = activeReservations
                    .Select(r => new ReservedTimeRangeDto
                    {
                        StartTimeUtc = r.StartTime,
                        EndTimeUtc = r.EndTime
                    })
                    .OrderBy(r => r.StartTimeUtc)
                    .ToList()
            })
            .ToList();

        var stationReservations = station.Reservations?
            .Where(IsUpcomingCustomerReservation)
            .OrderBy(r => r.StartTime)
            .Select(r => new StationReservationDto
            {
                StartTimeUtc = r.StartTime,
                EndTimeUtc = r.EndTime,
                Status = GetEffectiveStatus(r)
            })
            .ToList() ?? new List<StationReservationDto>();

        var dto = new StationDetailsDto
        {
            Id = station.Id,
            Name = station.Name.Translate() ?? station.Name.ToString() ?? string.Empty,
            Location = station.Location,
            Status = station.Status,
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
        var station = await _unitOfWork.ChargingStations.GetByIdWithDetailsAsync(dto.StationId);
        if (station == null)
        {
            return ServiceResult<ReservationDto>.Fail("NOT_FOUND", "Charging station not found.");
        }

        if (station.Status == EStationStatus.Maintenance)
        {
            return ServiceResult<ReservationDto>.Fail("STATION_UNAVAILABLE", "Station is currently unavailable for reservations.");
        }

        var startUtc = ToUtc(dto.StartTimeUtc);
        var endUtc = ToUtc(dto.EndTimeUtc);

        if (startUtc < DateTime.UtcNow)
        {
            return ServiceResult<ReservationDto>.Fail("VALIDATION", "Start time must be in the future.");
        }

        var overlapResult = await _availabilityService.ValidateOverlapAsync(station.Id, startUtc, endUtc);
        if (!overlapResult.Success)
        {
            return ServiceResult<ReservationDto>.Fail(overlapResult.Errors);
        }

        var durationMinutes = (int)Math.Ceiling((endUtc - startUtc).TotalMinutes);
        var estimateResult = await _pricingService.CalculateReservationEstimateAsync(station.Id, durationMinutes, dto.EstimatedEnergyKwh);
        if (!estimateResult.Success || estimateResult.Data == null)
        {
            return ServiceResult<ReservationDto>.Fail(estimateResult.Errors);
        }

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingStationId = station.Id,
            StartTime = startUtc,
            EndTime = endUtc,
            ExpiresAtUtc = startUtc.AddMinutes(15),
            EstimatedCost = estimateResult.Data.EstimatedCost,
            Status = EReservationStatus.Active
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

        await _unitOfWork.Reservations.AddAsync(reservation);
        await _unitOfWork.SaveAsync();

        var created = await _unitOfWork.Reservations.GetByIdForUserAsync(reservation.Id, userId);
        return created == null
            ? ServiceResult<ReservationDto>.Fail("NOT_FOUND", "Reservation could not be loaded after creation.")
            : ServiceResult<ReservationDto>.Ok(MapReservation(created));
    }

    public async Task<ServiceResult<List<ReservationDto>>> GetUserReservationsAsync(Guid userId)
    {
        var reservations = await _unitOfWork.Reservations.GetByUserIdAsync(userId);
        var filtered = reservations
            .Where(IsUpcomingCustomerReservation)
            .OrderBy(r => r.StartTime)
            .ToList();

        return ServiceResult<List<ReservationDto>>.Ok(filtered.Select(MapReservation).ToList());
    }

    public async Task<ServiceResult<ReservationDto>> GetReservationDetailsAsync(Guid reservationId, Guid userId)
    {
        var reservation = await _unitOfWork.Reservations.GetByIdForUserAsync(reservationId, userId);
        return reservation == null
            ? ServiceResult<ReservationDto>.Fail("FORBIDDEN", "Reservation not found or access denied.")
            : ServiceResult<ReservationDto>.Ok(MapReservation(reservation));
    }

    public async Task<ServiceResult> StartReservationAsync(Guid reservationId, Guid userId)
    {
        var reservation = await _unitOfWork.Reservations.GetByIdForUserAsync(reservationId, userId);
        if (reservation == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Reservation not found or access denied.");
        }

        var effectiveStatus = GetEffectiveStatus(reservation);
        if (effectiveStatus is EReservationStatus.Cancelled or EReservationStatus.Expired)
        {
            return ServiceResult.Fail("VALIDATION", "Reservation is already closed.");
        }

        if (reservation.Status == EReservationStatus.Started)
        {
            return ServiceResult.Fail("VALIDATION", "Reservation is already started.");
        }

        var nowUtc = DateTime.UtcNow;
        if (nowUtc < reservation.StartTime)
        {
            return ServiceResult.Fail("VALIDATION", "Reservation can be started only from its start time.");
        }

        if (nowUtc >= reservation.EndTime)
        {
            return ServiceResult.Fail("VALIDATION", "Reservation has already ended.");
        }

        reservation.Status = EReservationStatus.Started;
        reservation.ExpiresAtUtc = reservation.EndTime;

        // Prefer the tracked navigation entity from reservation lookup.
        if (reservation.ChargingStation != null)
        {
            reservation.ChargingStation.Status = EStationStatus.InUse;
        }
        else
        {
            var station = await _unitOfWork.ChargingStations.GetByIdWithDetailsAsync(reservation.ChargingStationId);
            if (station != null)
            {
                station.Status = EStationStatus.InUse;
            }
        }

        _unitOfWork.Reservations.Update(reservation);
        await _unitOfWork.SaveAsync();

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelReservationAsync(Guid reservationId, Guid userId)
    {
        var reservation = await _unitOfWork.Reservations.GetByIdForUserAsync(reservationId, userId);
        if (reservation == null)
        {
            return ServiceResult.Fail("FORBIDDEN", "Reservation not found or access denied.");
        }

        var effectiveStatus = GetEffectiveStatus(reservation);
        if (effectiveStatus is EReservationStatus.Cancelled or EReservationStatus.Expired or EReservationStatus.Started)
        {
            return ServiceResult.Fail("VALIDATION", "Reservation is already closed.");
        }

        reservation.Status = EReservationStatus.Cancelled;
        reservation.CancelledAtUtc = DateTime.UtcNow;
        _unitOfWork.Reservations.Update(reservation);
        await _unitOfWork.SaveAsync();

        return ServiceResult.Ok();
    }

    public Task<ServiceResult<CostEstimateDto>> EstimateCostAsync(Guid stationId, int durationMinutes, decimal? estimatedKwh = null)
    {
        return _pricingService.CalculateReservationEstimateAsync(stationId, durationMinutes, estimatedKwh);
    }

    private static ReservationDto MapReservation(Reservation reservation)
    {
        return new ReservationDto
        {
            Id = reservation.Id,
            StationId = reservation.ChargingStationId,
            StationName = reservation.ChargingStation?.Name.Translate() ?? reservation.ChargingStation?.Name.ToString() ?? string.Empty,
            StartTimeUtc = reservation.StartTime,
            EndTimeUtc = reservation.EndTime,
            ExpiresAtUtc = reservation.ExpiresAtUtc,
            CancelledAtUtc = reservation.CancelledAtUtc,
            EstimatedCost = reservation.EstimatedCost,
            Status = GetEffectiveStatus(reservation)
        };
    }

    private static EReservationStatus GetEffectiveStatus(Reservation reservation)
    {
        if (reservation.Status == EReservationStatus.Active && DateTime.UtcNow > reservation.ExpiresAtUtc)
        {
            return EReservationStatus.Expired;
        }

        return reservation.Status;
    }

    private static bool IsUpcomingCustomerReservation(Reservation reservation)
    {
        var effectiveStatus = GetEffectiveStatus(reservation);
        if (effectiveStatus is not EReservationStatus.Active and not EReservationStatus.Started)
        {
            return false;
        }

        return reservation.EndTime > DateTime.UtcNow;
    }

    private static bool IsBlockingReservation(Reservation reservation, DateTime nowUtc)
    {
        if (reservation.Status == EReservationStatus.Started)
        {
            return reservation.EndTime > nowUtc;
        }

        if (reservation.Status != EReservationStatus.Active)
        {
            return false;
        }

        return nowUtc <= reservation.ExpiresAtUtc && reservation.EndTime > nowUtc;
    }

    private static decimal ApplyDiscount(decimal baseCost, decimal discountValue)
    {
        var safeDiscount = Math.Min(100m, Math.Max(0m, discountValue));
        var discounted = baseCost * (1m - safeDiscount / 100m);
        return Math.Round(Math.Max(0m, discounted), 2, MidpointRounding.AwayFromZero);
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
