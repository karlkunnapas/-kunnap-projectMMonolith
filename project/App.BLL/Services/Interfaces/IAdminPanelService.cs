using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IAdminPanelService
{
    Task<ServiceResult<AdminDashboardDto>> GetDashboardAsync(DateTime fromUtc, DateTime toUtc);
    Task<ServiceResult<AdminCompanyListDto>> GetCompaniesAsync(string? search = null);
    Task<ServiceResult<AdminStationListDto>> GetStationsAsync(string? search = null);
    Task<ServiceResult<AdminCompanyListItemDto>> SetCompanyActivationAsync(Guid companyId, bool isActive, string actorUserName);
    Task<ServiceResult<AdminAuditLogListDto>> GetAuditLogsAsync(AdminAuditLogFilterDto filter);
}
