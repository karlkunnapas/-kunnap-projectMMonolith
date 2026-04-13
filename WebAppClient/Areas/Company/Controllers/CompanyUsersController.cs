using Microsoft.AspNetCore.Mvc;
using WebApp.Areas.Company.ViewModels;
using WebAppClient.Helpers;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Areas.Company.Controllers;

public class CompanyUsersController : CompanyBaseController
{
    public CompanyUsersController(IApiClient apiClient) : base(apiClient)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasOwnerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var users = await ApiClient.GetAsync<List<CompanyUserResponseDto>>($"api/v1/company/{company.Value.CompanyId}/users");
        return View(new CompanyUserListViewModel
        {
            CompanyId = company.Value.CompanyId,
            Users = users.Where(u => u.IsActive).Select(u => new CompanyUserItemViewModel
            {
                MembershipId = u.MembershipId,
                UserId = u.UserId,
                Email = u.Email,
                Role = EnumParser.ParseCompanyRole(u.Role),
                IsActive = u.IsActive,
                JoinedAtUtc = u.JoinedAtUtc
            }).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Add(Guid? companyId = null, string? returnUrl = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasOwnerAccess(company.Value.Role))
        {
            return Forbid();
        }

        return View(new AddCompanyUserViewModel
        {
            CompanyId = company.Value.CompanyId,
            ReturnUrl = returnUrl,
            Role = WebAppClient.Enums.ECompanyRole.Employee
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddCompanyUserViewModel model)
    {
        var company = await ResolveCompanyAsync(model.CompanyId);
        if (company == null || !HasOwnerAccess(company.Value.Role))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result = await ApiClient.PostAsync<AddCompanyUserResponseDto>(
                $"api/v1/company/{company.Value.CompanyId}/users",
                new AddCompanyUserRequestDto
                {
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber,
                    Password = model.Password,
                    ConfirmPassword = model.ConfirmPassword,
                    Role = (int)model.Role
                });

            TempData["AddResult"] = System.Text.Json.JsonSerializer.Serialize(new AddCompanyUserResultViewModel
            {
                CompanyId = company.Value.CompanyId,
                MembershipId = result.MembershipId,
                UserId = result.UserId,
                Email = result.Email,
                Role = EnumParser.ParseCompanyRole(result.Role),
                IsExistingUser = result.IsExistingUser,
                AccessStatus = result.AccessStatus,
                NextAction = result.NextAction
            });

            return RedirectToAction(nameof(AddResult), new { companyId = company.Value.CompanyId });
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> AddResult(Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasOwnerAccess(company.Value.Role))
        {
            return Forbid();
        }

        if (TempData.TryGetValue("AddResult", out var payloadObj)
            && payloadObj is string payload
            && !string.IsNullOrWhiteSpace(payload))
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<AddCompanyUserResultViewModel>(payload);
            if (result != null)
            {
                result.CompanyId = company.Value.CompanyId;
                return View(result);
            }
        }

        return RedirectToAction(nameof(Index), new { companyId = company.Value.CompanyId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid membershipId, Guid? companyId = null)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasOwnerAccess(company.Value.Role))
        {
            return Forbid();
        }

        var membership = await ApiClient.GetAsync<CompanyUserResponseDto>($"api/v1/company/{company.Value.CompanyId}/users/{membershipId}");
        return View(new EditCompanyUserRoleViewModel
        {
            CompanyId = company.Value.CompanyId,
            MembershipId = membership.MembershipId,
            UserId = membership.UserId,
            Email = membership.Email,
            Role = EnumParser.ParseCompanyRole(membership.Role)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditCompanyUserRoleViewModel model)
    {
        var company = await ResolveCompanyAsync(model.CompanyId);
        if (company == null || !HasOwnerAccess(company.Value.Role))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await ApiClient.PutAsync(
            $"api/v1/company/{company.Value.CompanyId}/users/{model.MembershipId}/role",
            new UpdateCompanyUserRoleRequestDto { Role = (int)model.Role });
        return RedirectToAction(nameof(Index), new { companyId = company.Value.CompanyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid companyId, Guid membershipId)
    {
        var company = await ResolveCompanyAsync(companyId);
        if (company == null || !HasOwnerAccess(company.Value.Role))
        {
            return Forbid();
        }

        await ApiClient.DeleteAsync($"api/v1/company/{company.Value.CompanyId}/users/{membershipId}");
        return RedirectToAction(nameof(Index), new { companyId = company.Value.CompanyId });
    }
}
