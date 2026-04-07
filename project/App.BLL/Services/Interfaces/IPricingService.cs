using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IPricingService
{
    Task<ServiceResult<CostEstimateDto>> CalculateReservationEstimateAsync(Guid stationId, int durationMinutes, decimal? estimatedEnergyKwh = null);
    Task<ServiceResult<decimal>> CalculateSessionFinalCostAsync(Guid stationId, decimal energyKwhConsumed, int durationMinutes);
}

