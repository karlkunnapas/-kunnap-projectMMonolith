using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IAvailabilityService
{
    Task<ServiceResult<List<AvailabilitySlotDto>>> GetAvailableSlotsAsync(Guid stationId, DateTime dateUtc, int durationMinutes);
    Task<ServiceResult> ValidateOverlapAsync(Guid stationId, DateTime startTimeUtc, DateTime endTimeUtc, Guid? excludeReservationId = null);
}

