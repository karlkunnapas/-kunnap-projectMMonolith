using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Companies;
using Shared.Contracts.Tenancy;
using Shared.Contracts.Users;
using WebApp.Areas.Company.ViewModels;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[Authorize(Roles = "CompanyOwner")]
public class CompanyUsersController : Controller
{
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private readonly IUsersModuleApi _usersModuleApi;
    private readonly ITenantContext _tenantContext;

    public CompanyUsersController(
        ICompaniesModuleApi companiesModuleApi,
        IUsersModuleApi usersModuleApi,
        ITenantContext tenantContext)
    {
        _companiesModuleApi = companiesModuleApi;
        _usersModuleApi = usersModuleApi;
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

        if (!await _companiesModuleApi.HasCompanyRoleAsync(resolvedCompany.Value, ownerUserId.Value, "Owner"))
        {
            return Forbid();
        }

        var memberships = await _companiesModuleApi.GetCompanyMembershipsAsync(resolvedCompany.Value);
        var users = new List<CompanyUserItemViewModel>();
        foreach (var membership in memberships.Where(m => m.IsActive))
        {
            var profile = await _usersModuleApi.GetUserProfileAsync(membership.UserId);
            users.Add(new CompanyUserItemViewModel
            {
                MembershipId = membership.MembershipId,
                UserId = membership.UserId,
                Email = profile?.Email ?? string.Empty,
                Role = NormalizeRole(membership.Role),
                IsActive = membership.IsActive,
                JoinedAtUtc = membership.JoinedAtUtc
            });
        }

        return View(new CompanyUserListViewModel
        {
            CompanyId = resolvedCompany.Value,
            Users = users
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
            Role = 2
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

        if (!await _companiesModuleApi.HasCompanyRoleAsync(resolvedCompany.Value, ownerUserId.Value, "Owner"))
        {
            return Forbid();
        }

        var postedRole = Request.HasFormContentType ? Request.Form["Role"].FirstOrDefault() : null;
        var normalizedRole = NormalizeRole(postedRole) ?? ToContractRole(model.Role);

        var result = await _companiesModuleApi.AddCompanyUserAsync(new AddCompanyUserContract
        {
            CompanyId = resolvedCompany.Value,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            PhoneNumber = model.PhoneNumber,
            Password = model.Password,
            Role = normalizedRole
        });

        if (!result.Success)
        {
            if (result.ErrorCode is "NOT_OWNER" or "FORBIDDEN")
            {
                return Forbid();
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to add company user.");

            model.CompanyId = resolvedCompany.Value;
            return View(model);
        }

        TempData["AddResult"] = System.Text.Json.JsonSerializer.Serialize(new AddCompanyUserResultViewModel
        {
            CompanyId = resolvedCompany.Value,
            MembershipId = result.MembershipId,
            UserId = result.UserId,
            Email = result.Email,
            Role = NormalizeRole(result.Role),
            IsExistingUser = result.IsExistingUser,
            MembershipReactivated = false,
            MembershipAlreadyActive = false,
            AccessStatus = result.AccessStatus,
            NextAction = result.NextAction
        });

        if (result.IsExistingUser)
        {
            await _companiesModuleApi.LogAuditMutationAsync(
                resolvedCompany.Value,
                User.Identity?.Name ?? ownerUserId.Value.ToString(),
                "AppUserCompany",
                result.MembershipId,
                "ExistingUserLinked");
        }

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

        if (!await _companiesModuleApi.HasCompanyRoleAsync(resolvedCompany.Value, ownerUserId.Value, "Owner"))
        {
            return Forbid();
        }

        var membership = await _companiesModuleApi.GetCompanyMembershipAsync(resolvedCompany.Value, membershipId);
        if (membership == null)
        {
            return NotFound();
        }

        var profile = await _usersModuleApi.GetUserProfileAsync(membership.UserId);

        return View(new EditCompanyUserRoleViewModel
        {
            CompanyId = resolvedCompany.Value,
            MembershipId = membership.MembershipId,
            UserId = membership.UserId,
            Email = profile?.Email ?? string.Empty,
            Role = ParseRoleOrDefault(membership.Role)
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

        if (!await _companiesModuleApi.HasCompanyRoleAsync(resolvedCompany.Value, ownerUserId.Value, "Owner"))
        {
            return Forbid();
        }

        var postedRole = Request.HasFormContentType ? Request.Form["Role"].FirstOrDefault() : null;
        var normalizedRole = NormalizeRole(postedRole) ?? ToContractRole(model.Role);

        var result = await _companiesModuleApi.UpdateCompanyMembershipRoleWithGuardsAsync(
            resolvedCompany.Value,
            model.MembershipId,
            normalizedRole);

        if (!result.Success)
        {
            if (result.ErrorCode is "NOT_OWNER" or "FORBIDDEN")
            {
                return Forbid();
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Unable to update company user role.");
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

        if (!await _companiesModuleApi.HasCompanyRoleAsync(resolvedCompany.Value, ownerUserId.Value, "Owner"))
        {
            return Forbid();
        }

        var result = await _companiesModuleApi.DeactivateCompanyMembershipWithGuardsAsync(
            resolvedCompany.Value,
            membershipId);

        if (!result.Success)
        {
            if (result.ErrorCode is "NOT_OWNER" or "FORBIDDEN")
            {
                return Forbid();
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                TempData["CompanyUsersError"] = result.ErrorMessage;
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

        var memberships = await _companiesModuleApi.GetUserCompaniesAsync(userId.Value);
        return memberships
            .Where(m => string.Equals(m.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.MembershipId)
            .Select(m => m.CompanyId)
            .ToList();
    }

    private Guid? ResolveCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId) ? userId : null;
    }

    private static int ParseRoleOrDefault(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "owner" => 0,
            "manager" => 1,
            _ => 2
        };
    }

    private static string ToContractRole(int role)
    {
        return role switch
        {
            0 => "Owner",
            1 => "Manager",
            _ => "Employee"
        };
    }

    private static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        var normalized = role.Trim();
        if (int.TryParse(normalized, out var roleInt))
        {
            return roleInt switch
            {
                0 => "Owner",
                1 => "Manager",
                2 => "Employee",
                _ => null
            };
        }

        return normalized.ToLowerInvariant() switch
        {
            "owner" => "Owner",
            "manager" => "Manager",
            "employee" => "Employee",
            _ => null
        };
    }
}
