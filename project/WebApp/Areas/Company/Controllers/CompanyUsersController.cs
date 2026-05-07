using System.Security.Claims;
using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.DAL.EF;
using App.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.Areas.Company.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize(Roles = "CompanyOwner")]
public class CompanyUsersController : Controller
{
    private readonly IIdentityService _identityService;
    private readonly AppDbContext _context;
    private readonly ITenantContext _tenantContext;

    public CompanyUsersController(IIdentityService identityService, AppDbContext context, ITenantContext tenantContext)
    {
        _identityService = identityService;
        _context = context;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        var ownerUserId = ResolveCurrentUserId();
        if (resolvedCompany == null || ownerUserId == null)
        {
            return Forbid();
        }

        var result = await _identityService.GetCompanyUsersAsync(resolvedCompany.Value, ownerUserId.Value);
        if (!result.Success)
        {
            return Forbid();
        }

        return View(new CompanyUserListViewModel
        {
            CompanyId = resolvedCompany.Value,
            Users = result.Data?.Select(user => new CompanyUserItemViewModel
            {
                MembershipId = user.MembershipId,
                UserId = user.UserId,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                JoinedAtUtc = user.JoinedAtUtc
            }).Where(user => user.IsActive).ToList() ?? new List<CompanyUserItemViewModel>()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Add(Guid? companyId = null, string? returnUrl = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
        {
            return Forbid();
        }

        return View(new AddCompanyUserViewModel
        {
            CompanyId = resolvedCompany.Value,
            ReturnUrl = returnUrl,
            Role = ECompanyRole.Employee
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddCompanyUserViewModel model)
    {
        var resolvedCompany = await ResolveCompanyAsync(model.CompanyId);
        var ownerUserId = ResolveCurrentUserId();
        if (resolvedCompany == null || ownerUserId == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.CompanyId = resolvedCompany.Value;
            return View(model);
        }

        var result = await _identityService.AddUserToCompanyAsync(
            resolvedCompany.Value,
            ownerUserId.Value,
            User.Identity?.Name ?? ownerUserId.Value.ToString(),
            BllDtoFactory.CreateAddCompanyUserRequestDto(
                model.Email,
                model.Role,
                model.FirstName,
                model.LastName,
                model.PhoneNumber,
                model.Password,
                model.ConfirmPassword));

        if (!result.Success || result.Data == null)
        {
            if (result.Errors.Any(error => error.Code is "NOT_OWNER" or "FORBIDDEN"))
            {
                return Forbid();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }

            model.CompanyId = resolvedCompany.Value;
            return View(model);
        }

        TempData["AddResult"] = System.Text.Json.JsonSerializer.Serialize(new AddCompanyUserResultViewModel
        {
            CompanyId = result.Data.CompanyId,
            MembershipId = result.Data.MembershipId,
            UserId = result.Data.UserId,
            Email = result.Data.Email,
            Role = result.Data.Role,
            IsExistingUser = result.Data.IsExistingUser,
            MembershipReactivated = result.Data.MembershipReactivated,
            MembershipAlreadyActive = result.Data.MembershipAlreadyActive,
            AccessStatus = result.Data.AccessStatus,
            NextAction = result.Data.NextAction
        });

        return RedirectToAction(nameof(AddResult), new { companyId = resolvedCompany.Value });
    }

    [HttpGet]
    public async Task<IActionResult> AddResult(Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        if (resolvedCompany == null)
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
                result.CompanyId = resolvedCompany.Value;
                return View(result);
            }
        }

        return RedirectToAction(nameof(Index), new { companyId = resolvedCompany.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid membershipId, Guid? companyId = null)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        var ownerUserId = ResolveCurrentUserId();
        if (resolvedCompany == null || ownerUserId == null)
        {
            return Forbid();
        }

        var membershipResult = await _identityService.GetCompanyUserMembershipAsync(
            resolvedCompany.Value,
            ownerUserId.Value,
            membershipId);

        if (!membershipResult.Success || membershipResult.Data == null)
        {
            return membershipResult.Errors.Any(e => e.Code is "NOT_OWNER" or "FORBIDDEN") ? Forbid() : NotFound();
        }

        return View(new EditCompanyUserRoleViewModel
        {
            CompanyId = resolvedCompany.Value,
            MembershipId = membershipResult.Data.MembershipId,
            UserId = membershipResult.Data.UserId,
            Email = membershipResult.Data.Email,
            Role = membershipResult.Data.Role
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditCompanyUserRoleViewModel model)
    {
        var resolvedCompany = await ResolveCompanyAsync(model.CompanyId);
        var ownerUserId = ResolveCurrentUserId();
        if (resolvedCompany == null || ownerUserId == null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.CompanyId = resolvedCompany.Value;
            return View(model);
        }

        var result = await _identityService.UpdateCompanyUserRoleAsync(
            resolvedCompany.Value,
            ownerUserId.Value,
            User.Identity?.Name ?? ownerUserId.Value.ToString(),
            model.MembershipId,
            BllDtoFactory.CreateUpdateCompanyUserRoleRequestDto(model.Role));

        if (!result.Success)
        {
            if (result.Errors.Any(e => e.Code is "NOT_OWNER" or "FORBIDDEN"))
            {
                return Forbid();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }

            return View(model);
        }

        return RedirectToAction(nameof(Index), new { companyId = resolvedCompany.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid companyId, Guid membershipId)
    {
        var resolvedCompany = await ResolveCompanyAsync(companyId);
        var ownerUserId = ResolveCurrentUserId();
        if (resolvedCompany == null || ownerUserId == null)
        {
            return Forbid();
        }

        var result = await _identityService.RemoveCompanyUserAsync(
            resolvedCompany.Value,
            ownerUserId.Value,
            User.Identity?.Name ?? ownerUserId.Value.ToString(),
            membershipId);

        if (!result.Success)
        {
            if (result.Errors.Any(e => e.Code is "NOT_OWNER" or "FORBIDDEN"))
            {
                return Forbid();
            }

            var firstError = result.Errors.FirstOrDefault();
            if (firstError != null)
            {
                TempData["CompanyUsersError"] = firstError.Message;
            }
        }

        return RedirectToAction(nameof(Index), new { companyId = resolvedCompany.Value });
    }

    private async Task<Guid?> ResolveCompanyAsync(Guid? requestedCompanyId)
    {
        var membershipCompanyIds = await ResolveOwnerMembershipCompanyIdsAsync();
        if (membershipCompanyIds.Count == 0)
        {
            return null;
        }

        var resolvedCompanyId = requestedCompanyId ?? _tenantContext.CompanyId ?? membershipCompanyIds[0];
        return membershipCompanyIds.Contains(resolvedCompanyId) ? resolvedCompanyId : null;
    }

    private async Task<List<Guid>> ResolveOwnerMembershipCompanyIdsAsync()
    {
        var userId = ResolveCurrentUserId();
        if (userId == null)
        {
            return new List<Guid>();
        }

        return await _context.AppUserCompanies
            .AsNoTracking()
            .Where(uc => uc.AppUserId == userId.Value && uc.IsActive && uc.Company != null && uc.Company.IsActive && uc.Role == ECompanyRole.Owner)
            .OrderByDescending(uc => uc.JoinedAtUtc)
            .Select(uc => uc.CompanyId)
            .ToListAsync();
    }

    private Guid? ResolveCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }
}
