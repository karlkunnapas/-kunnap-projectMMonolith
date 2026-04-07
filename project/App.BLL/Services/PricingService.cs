using App.BLL.DTOs;
using App.BLL.Services.Interfaces;
using App.DAL.EF.Repositories.Interfaces;

namespace App.BLL.Services;

public class PricingService : IPricingService
{
    private readonly IUnitOfWork _unitOfWork;

    public PricingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<CostEstimateDto>> CalculateReservationEstimateAsync(Guid stationId, int durationMinutes, decimal? estimatedEnergyKwh = null)
    {
        if (durationMinutes <= 0)
        {
            return ServiceResult<CostEstimateDto>.Fail("VALIDATION", "Duration must be greater than zero.");
        }

        var station = await _unitOfWork.ChargingStations.GetByIdWithDetailsAsync(stationId);
        if (station == null)
        {
            return ServiceResult<CostEstimateDto>.Fail("NOT_FOUND", "Charging station not found.");
        }

        var durationHours = durationMinutes / 60m;
        var effectiveEnergyKwh = estimatedEnergyKwh.GetValueOrDefault();
        if (effectiveEnergyKwh <= 0)
        {
            // Fallback estimate when client does not pass energy: derive from time and capped station power.
            var effectivePower = Math.Max(1m, Math.Min(station.MaxPower, 200m));
            effectiveEnergyKwh = durationHours * effectivePower;
        }

        var estimatedCost = Math.Round(station.PricePerKwh * effectiveEnergyKwh, 2, MidpointRounding.AwayFromZero);

        return ServiceResult<CostEstimateDto>.Ok(new CostEstimateDto
        {
            DurationMinutes = durationMinutes,
            EstimatedCost = estimatedCost
        });
    }

    public async Task<ServiceResult<decimal>> CalculateSessionFinalCostAsync(Guid stationId, decimal energyKwhConsumed, int durationMinutes)
    {
        var estimateResult = await CalculateReservationEstimateAsync(stationId, durationMinutes, energyKwhConsumed);
        if (!estimateResult.Success || estimateResult.Data == null)
        {
            return ServiceResult<decimal>.Fail(estimateResult.Errors);
        }

        return ServiceResult<decimal>.Ok(estimateResult.Data.EstimatedCost);
    }
}


