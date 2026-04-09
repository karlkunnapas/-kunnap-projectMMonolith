using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IChargingStationCompanyService
{
    Task<ServiceResult<List<CompanyStationDto>>> GetCompanyStationsAsync(Guid companyId);
    Task<ServiceResult<CompanyStationDto>> GetStationDetailsAsync(Guid stationId, Guid companyId);
}
