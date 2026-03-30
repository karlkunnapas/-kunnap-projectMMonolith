using App.BLL.DTOs;
using Microsoft.AspNetCore.Identity;

namespace App.BLL.Services.Interfaces;

public interface IIdentityService
{
    Task<SignInResult> LoginAsync(string email, string password, bool rememberMe);
    Task LogoutAsync();
    Task<ServiceResult<UserCompanyListResultDto>> GetUserCompaniesAsync(Guid userId);
    Task<ServiceResult<Guid>> RegisterCompanyOwnerAsync(RegisterCompanyOwnerDto dto);
    Task<ServiceResult<Guid>> RegisterCustomerAsync(RegisterCustomerDto dto);
}