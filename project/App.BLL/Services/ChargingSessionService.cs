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

    public ChargingSessionService(IUnitOfWork unitOfWork, IReservationService reservationService, IPricingService pricingService)
    {
        _unitOfWork = unitOfWork;
        _reservationService = reservationService;
        _pricingService = pricingService;
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
            Cost = calculatedCost,
            IsActive = session.EndTime == null
        };
    }

    private static decimal CalculateEnergyEstimateKwh(int durationMinutes, decimal? stationMaxPower)
    {
        var effectivePower = Math.Max(1m, Math.Min(stationMaxPower ?? 50m, 200m));
        var durationHours = durationMinutes / 60m;
        return Math.Round(durationHours * effectivePower, 2, MidpointRounding.AwayFromZero);
    }
}
