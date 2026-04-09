using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IChargingSessionService
{
    Task<ServiceResult<ChargingSessionDto>> StartSessionAsync(Guid userId, ChargingSessionStartRequestDto dto);
    Task<ServiceResult<ChargingSessionDto>> StopSessionAsync(Guid userId, Guid sessionId, ChargingSessionStopRequestDto dto);
    Task<ServiceResult<ChargingSessionDetailsDto>> GetSessionDetailsAsync(Guid sessionId, Guid userId);
    Task<ServiceResult<List<ChargingSessionDto>>> GetUserSessionsAsync(Guid userId);
    Task<ServiceResult<decimal>> CalculateFinalCostAsync(Guid sessionId);
}
