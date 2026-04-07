using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IReservationService
{
    Task<ServiceResult<StationDetailsDto>> GetStationDetailsAsync(Guid stationId, DateTime? dateUtc = null);
    Task<ServiceResult<ReservationDto>> ReserveAsync(Guid userId, ReservationCreateDto dto);
    Task<ServiceResult<List<ReservationDto>>> GetUserReservationsAsync(Guid userId);
    Task<ServiceResult<ReservationDto>> GetReservationDetailsAsync(Guid reservationId, Guid userId);
    Task<ServiceResult> StartReservationAsync(Guid reservationId, Guid userId);
    Task<ServiceResult> CancelReservationAsync(Guid reservationId, Guid userId);
    Task<ServiceResult<CostEstimateDto>> EstimateCostAsync(Guid stationId, int durationMinutes, decimal? estimatedKwh = null);
}
