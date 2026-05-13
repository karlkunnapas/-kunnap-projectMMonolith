using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using Shared.Contracts.Charging;

namespace App.BLL.Services;

public class AvailabilityService : IAvailabilityService
{
    private readonly IChargingModuleApi _chargingModuleApi;

    public AvailabilityService(IChargingModuleApi chargingModuleApi)
    {
        _chargingModuleApi = chargingModuleApi;
    }

    public async Task<ServiceResult<List<AvailabilitySlotDto>>> GetAvailableSlotsAsync(Guid stationId, DateTime dateUtc, int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            return ServiceResult<List<AvailabilitySlotDto>>.Fail("VALIDATION", "Duration must be greater than zero.");
        }

        var dayStart = DateTime.SpecifyKind(dateUtc.Date, DateTimeKind.Utc);
        var slots = new List<AvailabilitySlotDto>();
        var windowHours = new[] { 14, 18, 22 };

        for (var dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            foreach (var hour in windowHours)
            {
                var start = dayStart.AddDays(dayOffset).AddHours(hour);
                var end = start.AddMinutes(durationMinutes);
                var overlapCount = await GetOverlappingCountAsync(stationId, start, end);
                slots.Add(BllDtoFactory.CreateAvailabilitySlotDto(start, end, overlapCount == 0));
            }
        }

        return ServiceResult<List<AvailabilitySlotDto>>.Ok(slots);
    }

    public async Task<ServiceResult> ValidateOverlapAsync(Guid stationId, DateTime startTimeUtc, DateTime endTimeUtc, Guid? excludeReservationId = null)
    {
        if (startTimeUtc.Kind == DateTimeKind.Unspecified || endTimeUtc.Kind == DateTimeKind.Unspecified)
        {
            return ServiceResult.Fail("VALIDATION", "Date/time values must be provided in UTC.");
        }

        if (startTimeUtc >= endTimeUtc)
        {
            return ServiceResult.Fail("VALIDATION", "Start time must be before end time.");
        }

        var overlaps = await GetOverlappingCountAsync(stationId, startTimeUtc, endTimeUtc, excludeReservationId);
        return overlaps > 0
            ? ServiceResult.Fail("OVERLAP", "The selected time window overlaps with an existing reservation.")
            : ServiceResult.Ok();
    }

    private async Task<int> GetOverlappingCountAsync(Guid stationId, DateTime startTimeUtc, DateTime endTimeUtc, Guid? excludeReservationId = null)
    {
        var overlaps = await _chargingModuleApi.GetOverlappingReservationsAsync(
            stationId,
            startTimeUtc,
            endTimeUtc,
            excludeReservationId);
        return overlaps.Count;
    }
}
