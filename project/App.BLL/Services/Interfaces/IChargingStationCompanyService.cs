using App.BLL.DTOs;
using App.Domain;

namespace App.BLL.Services.Interfaces;

public interface IChargingStationCompanyService
{
    Task<ServiceResult<List<CompanyStationDto>>> GetCompanyStationsAsync(Guid companyId);
    Task<ServiceResult<CompanyStationDto>> GetStationDetailsAsync(Guid stationId, Guid companyId);
    Task<ServiceResult<CompanyStationFormDto>> GetCreateFormAsync(Guid companyId);
    Task<ServiceResult<CompanyStationFormDto>> GetEditFormAsync(Guid stationId, Guid companyId);
    Task<ServiceResult<CompanyStationDto>> CreateStationAsync(Guid companyId, Guid userId, string userName, CompanyStationUpsertDto dto);
    Task<ServiceResult<CompanyStationDto>> UpdateStationAsync(Guid stationId, Guid companyId, Guid userId, string userName, CompanyStationUpsertDto dto);
    Task<ServiceResult> DeleteStationAsync(Guid stationId, Guid companyId, Guid userId, string userName);
    Task<ServiceResult<CompanyStationDto>> UpdateStatusAsync(Guid stationId, Guid companyId, Guid userId, string userName, EStationStatus status);
    Task<ServiceResult> AssignConnectorAsync(Guid stationId, Guid companyId, Guid userId, string userName, Guid connectorId);
    Task<ServiceResult> RemoveConnectorAsync(Guid stationId, Guid companyId, Guid userId, string userName, Guid connectorId);
}
