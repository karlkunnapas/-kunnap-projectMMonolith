using App.BLL.DTOs;
using Microsoft.AspNetCore.Identity;

namespace App.BLL.Services.Interfaces;

public interface IIdentityService
{
    Task<SignInResult> LoginAsync(string email, string password, bool rememberMe);
    Task LogoutAsync();
    Task<ServiceResult<UserCompanyListResultDto>> GetUserCompaniesAsync(Guid userId);
    Task<ServiceResult<List<CompanyUserMembershipDto>>> GetCompanyUsersAsync(Guid companyId, Guid ownerUserId);
    Task<ServiceResult<CompanyUserMembershipDto>> GetCompanyUserMembershipAsync(Guid companyId, Guid ownerUserId, Guid membershipId);
    Task<ServiceResult<AddCompanyUserResultDto>> AddUserToCompanyAsync(
        Guid companyId,
        Guid ownerUserId,
        string ownerUserName,
        AddCompanyUserRequestDto dto);
    Task<ServiceResult<CompanyUserMembershipDto>> UpdateCompanyUserRoleAsync(
        Guid companyId,
        Guid ownerUserId,
        string ownerUserName,
        Guid membershipId,
        UpdateCompanyUserRoleRequestDto dto);
    Task<ServiceResult> RemoveCompanyUserAsync(
        Guid companyId,
        Guid ownerUserId,
        string ownerUserName,
        Guid membershipId);
    Task<ServiceResult<CompanySelectionItemDto>> SetActiveCompanyAsync(Guid userId, Guid companyId, string actorUserName);
    Task<ServiceResult<Guid>> RegisterCompanyOwnerAsync(RegisterCompanyOwnerDto dto);
    Task<ServiceResult<Guid>> RegisterCustomerAsync(RegisterCustomerDto dto);
}
