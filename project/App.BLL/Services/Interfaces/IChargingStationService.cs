using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IChargingStationService
{
    Task<ServiceResult<HomePageDto>> GetHomePageAsync(HomePageFilterDto? filters = null);
}
