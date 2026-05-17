using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Modules.Users.Domain;
using Shared.Contracts;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;
using WebApp.ViewModels.Account;

namespace WebApp.Controllers;

public class AccountController : Controller
{
    private readonly IUsersModuleApi _usersModuleApi;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private static string R(string key) =>
        App.Resources.Views.Account.Register.ResourceManager.GetString(key) ?? key;

    public AccountController(
        IUsersModuleApi usersModuleApi,
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        ICompaniesModuleApi companiesModuleApi)
    {
        _usersModuleApi = usersModuleApi;
        _signInManager = signInManager;
        _userManager = userManager;
        _companiesModuleApi = companiesModuleApi;
    }

    // GET: /Account/Register (customer)
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        return View(new CustomerRegisterViewModel
        {
            ReturnUrl = returnUrl,
            CompanyRegisterUrl = Url.Action("Register", "Account", new { area = "Company" })
        });
    }

    // POST: /Account/Register (customer)
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(CustomerRegisterViewModel model, string? returnUrl = null)
    {
        model.ReturnUrl = returnUrl;
        model.CompanyRegisterUrl = Url.Action("Register", "Account", new { area = "Company" });

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _usersModuleApi.RegisterCustomerAsync(new RegisterCustomerContract
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            Password = model.Password
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? R("RegistrationFailed"));
            return View(model);
        }

        // Auto-login after registration
        await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, lockoutOnFailure: false);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    // GET: /Account/Login
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    // POST: /Account/Login
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var signInResult = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (!signInResult.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                R("InvalidLoginAttempt"));
            return View(model);
        }

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        var companies = (await _companiesModuleApi.GetUserCompaniesAsync(userId.Value)).ToList();
        if (companies.Count > 0)
        {
            if (companies.Count == 1)
            {
                var company = companies[0];
                return RedirectToCompanyHome(company.Slug, company.Role);
            }

            return RedirectToAction(nameof(CompanySelection), new { returnUrl = model.ReturnUrl });
        }

        if (await HasDeactivatedCompanyMembershipAsync(userId.Value))
        {
            return RedirectToAction(nameof(CompanyDeactivated));
        }

        var loggedInUser = await _userManager.FindByEmailAsync(model.Email);
        if (loggedInUser != null)
        {
            var roles = await _userManager.GetRolesAsync(loggedInUser);
            if (roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(r, "root", StringComparison.OrdinalIgnoreCase)))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    // GET: /Account/CompanySelection
    [Authorize]
    public async Task<IActionResult> CompanySelection(string? returnUrl = null)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return RedirectToAction(nameof(Login));
        }

        // Check if user is a System Admin - they don't need company selection
        if (User.IsInRole("SystemAdmin") || User.IsInRole("SystemSupport") || User.IsInRole("SystemBilling"))
        {
            return Redirect("/System/Companies");
        }

        var companies = (await _companiesModuleApi.GetUserCompaniesAsync(userId.Value)).ToList();
        if (companies.Count == 0)
        {
            if (await HasDeactivatedCompanyMembershipAsync(userId.Value))
            {
                return RedirectToAction(nameof(CompanyDeactivated));
            }

            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        // If user has only one company, redirect directly
        if (companies.Count == 1)
        {
            var company = companies.First();
                return RedirectToCompanyHome(company.Slug, company.Role);
        }

        var viewModel = new CompanySelectionViewModel
        {
            ReturnUrl = returnUrl,
            Companies = companies.Select(c => new CompanySelectionItemViewModel
            {
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName,
                CompanySlug = c.Slug,
                Role = c.Role
            }).ToList(),
            SelectedCompanyId = companies.First().CompanyId
        };

        return View(viewModel);
    }

    // POST: /Account/CompanySelection
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompanySelection(CompanySelectionViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return RedirectToAction(nameof(Login));
        }

        // Reload companies from database (model.Companies is empty from form post)
        var companies = (await _companiesModuleApi.GetUserCompaniesAsync(userId.Value)).ToList();
        if (companies.Count == 0)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        var selectedCompanyId = model.SelectedCompanyId;
        var company = companies.FirstOrDefault(c => c.CompanyId == selectedCompanyId);
        if (company == null || !ModelState.IsValid)
        {
            // Reload the model with companies and show error
            model.Companies = companies.Select(c => new CompanySelectionItemViewModel
            {
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName,
                CompanySlug = c.Slug,
                Role = c.Role
            }).ToList();
            
            ModelState.AddModelError(string.Empty, R("InvalidCompanySelection"));
            return View(model);
        }

        var actorUserName = User.Identity?.Name
            ?? User.FindFirstValue(ClaimTypes.Email)
            ?? userId.Value.ToString();

        var activeCompany = await _companiesModuleApi.SelectActiveCompanyAsync(userId.Value, company.CompanyId, actorUserName);
        if (activeCompany == null)
        {
            return Forbid();
        }

        var companySlugs = companies
            .Select(c => c.Slug)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalizedReturnUrl = NormalizeCompanyReturnUrl(
            model.ReturnUrl,
            activeCompany.Slug,
            companySlugs);

        if (!string.IsNullOrWhiteSpace(normalizedReturnUrl) && Url.IsLocalUrl(normalizedReturnUrl))
        {
            return Redirect(normalizedReturnUrl);
        }

        return RedirectToCompanyHome(activeCompany.Slug, activeCompany.Role);
    }

    // POST: /Account/SwitchCompany
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchCompany(Guid companyId, string? returnUrl = null)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return RedirectToAction(nameof(Login));
        }

        var activeCompany = await _companiesModuleApi.GetActiveCompanySelectionAsync(userId.Value, companyId);
        if (activeCompany == null)
        {
            return Forbid();
        }

        var companies = await _companiesModuleApi.GetUserCompaniesAsync(userId.Value);
        var companySlugs = companies
                .Select(c => c.Slug)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ;

        var normalizedReturnUrl = NormalizeCompanyReturnUrl(
            returnUrl,
            activeCompany.Slug,
            companySlugs);

        if (!string.IsNullOrWhiteSpace(normalizedReturnUrl) && Url.IsLocalUrl(normalizedReturnUrl))
        {
            return Redirect(normalizedReturnUrl);
        }

        return RedirectToCompanyHome(activeCompany.Slug, activeCompany.Role);
    }

    // POST: /Account/Logout
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(LoggedOut));
    }

    // GET: /Account/LoggedOut
    [AllowAnonymous]
    public IActionResult LoggedOut()
    {
        return View();
    }

    // GET: /Account/AccessDenied
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // GET: /Account/CompanyDeactivated
    [Authorize]
    public IActionResult CompanyDeactivated()
    {
        return View();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    private IActionResult RedirectToCompanyHome(string companySlug, string? role)
    {
        var isEmployee = string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase);
        return isEmployee
            ? RedirectToAction("Index", "Maintenance", new { area = "Company", companySlug })
            : RedirectToAction("Index", "Dashboard", new { area = "Company", companySlug });
    }

    private static string? NormalizeCompanyReturnUrl(string? returnUrl, string targetCompanySlug, ISet<string> knownCompanySlugs)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || string.IsNullOrWhiteSpace(targetCompanySlug))
        {
            return returnUrl;
        }

        var hashIndex = returnUrl.IndexOf('#');
        var hashPart = hashIndex >= 0 ? returnUrl[hashIndex..] : string.Empty;
        var pathAndQuery = hashIndex >= 0 ? returnUrl[..hashIndex] : returnUrl;

        var queryIndex = pathAndQuery.IndexOf('?');
        var pathPart = queryIndex >= 0 ? pathAndQuery[..queryIndex] : pathAndQuery;
        var queryPart = queryIndex >= 0 ? pathAndQuery[queryIndex..] : string.Empty;

        var segments = pathPart.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return returnUrl;
        }

        if (!knownCompanySlugs.Contains(segments[0]))
        {
            return returnUrl;
        }

        segments[0] = targetCompanySlug;
        return "/" + string.Join('/', segments) + queryPart + hashPart;
    }

    private async Task<bool> HasDeactivatedCompanyMembershipAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        return await _companiesModuleApi.HasDeactivatedActiveMembershipAsync(userId);
    }
}
