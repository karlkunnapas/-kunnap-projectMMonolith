using App.BLL.DTOs;
using App.Domain;

namespace App.BLL.Services.Interfaces;

public interface IMaintenanceService
{
    Task<ServiceResult<List<MaintenanceIssueDto>>> GetIssuesAsync(Guid companyId, bool includeResolved = true);
    Task<ServiceResult<MaintenanceIssueDto>> GetByIdAsync(Guid id, Guid companyId);
    Task<ServiceResult<MaintenanceIssueDto>> CreateMaintenanceAsync(Guid stationId, Guid companyId, Guid userId, string issueDescription);
    Task<ServiceResult<MaintenanceIssueDto>> UpdateStatusAsync(Guid id, Guid companyId, Guid userId, EMaintenanceStatus newStatus, string? notes);
    Task<ServiceResult<MaintenanceIssueDto>> AssignAsync(Guid id, Guid companyId, Guid userId, Guid? assignedToUserId);
    Task<ServiceResult<List<MaintenanceStatusHistoryDto>>> GetStatusHistoryAsync(Guid maintenanceId, Guid companyId);
}
