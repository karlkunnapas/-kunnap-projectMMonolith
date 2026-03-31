using App.BLL.DTOs;

namespace App.BLL.Services.Interfaces;

public interface IHomePageService
{
    Task<ServiceResult<HomePageDto>> GetCustomerHomePageAsync();
}

