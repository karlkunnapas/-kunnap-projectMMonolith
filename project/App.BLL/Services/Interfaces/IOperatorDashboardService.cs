using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IOperatorDashboardService
{
    Task<ServiceResult<OperatorDashboardDto>> GetDashboardAsync(Guid companyId, DateTime fromUtc, DateTime toUtc);
    Task<ServiceResult<List<CompanyStationStatusDto>>> GetStationStatusAsync(Guid companyId, DateTime fromUtc, DateTime toUtc);
    Task<ServiceResult<List<MaintenanceIssueDto>>> GetMaintenanceQueueAsync(Guid companyId);
    Task<ServiceResult<List<ChartPointDto>>> GetUtilizationTrendAsync(Guid companyId, DateTime fromUtc, DateTime toUtc);
    Task<ServiceResult<List<ChartPointDto>>> GetRevenueTrendAsync(Guid companyId, DateTime fromUtc, DateTime toUtc);
}
