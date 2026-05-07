using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IAdminPanelService
{
    Task<ServiceResult<AdminDashboardDto>> GetDashboardAsync(DateTime fromUtc, DateTime toUtc);
    Task<ServiceResult<AdminCompanyListDto>> GetCompaniesAsync(string? search = null);
    Task<ServiceResult<AdminStationListDto>> GetStationsAsync(string? search = null);
    Task<ServiceResult<AdminCompanyListItemDto>> SetCompanyActivationAsync(Guid companyId, bool isActive, string actorUserName);
    Task<ServiceResult<AdminAuditLogListDto>> GetAuditLogsAsync(AdminAuditLogFilterDto filter);
    Task<ServiceResult<AdminPromotionListDto>> GetSystemPromotionsAsync();
    Task<ServiceResult<AdminPromotionFormDto>> GetSystemPromotionAsync(Guid promotionId);
    Task<ServiceResult<AdminPromotionFormDto>> CreateSystemPromotionAsync(AdminPromotionFormDto dto, string actorUserName);
    Task<ServiceResult<AdminPromotionFormDto>> UpdateSystemPromotionAsync(Guid promotionId, AdminPromotionFormDto dto, string actorUserName);
    Task<ServiceResult> DeleteSystemPromotionAsync(Guid promotionId, string actorUserName);
    Task<ServiceResult<AdminConnectorTypeListDto>> GetConnectorTypesAsync(string? search = null);
    Task<ServiceResult<AdminConnectorTypeFormDto>> GetConnectorTypeAsync(Guid connectorTypeId);
    Task<ServiceResult<AdminConnectorTypeFormDto>> CreateConnectorTypeAsync(AdminConnectorTypeFormDto dto, string actorUserName);
    Task<ServiceResult<AdminConnectorTypeFormDto>> UpdateConnectorTypeAsync(Guid connectorTypeId, AdminConnectorTypeFormDto dto, string actorUserName);
    Task<ServiceResult> DeleteConnectorTypeAsync(Guid connectorTypeId, string actorUserName);
}
