using System.Security.Claims;
using App.BLL.DTOs;
using App.BLL.Mappers;
using App.BLL.Services.Interfaces;
using App.Domain;
using App.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels.Account;

namespace WebApp.Controllers;

public class AccountController : Controller
{
    private readonly IIdentityService _identityService;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;

    public AccountController(
        IIdentityService identityService,
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager)
    {
        _identityService = identityService;
        _signInManager = signInManager;
        _userManager = userManager;
    }

    // GET: /Account/Register (customer)
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["CompanyRegisterUrl"] = Url.Action("Register", "Account", new { area = "Company" });
        return View(new CustomerRegisterViewModel());
    }

    // POST: /Account/Register (customer)
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(CustomerRegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["CompanyRegisterUrl"] = Url.Action("Register", "Account", new { area = "Company" });

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var dto = BllDtoFactory.CreateRegisterCustomerDto(
            model.FirstName,
            model.LastName,
            model.Email,
            model.PhoneNumber,
            model.Password,
            model.ConfirmPassword);

        var result = await _identityService.RegisterCustomerAsync(dto);

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Message);
            }
            return View(model);
        }

        // Auto-login after registration
        await _identityService.LoginAsync(model.Email, model.Password, false);

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

        var signInResult = await _identityService.LoginAsync(model.Email, model.Password, model.RememberMe);
        if (!signInResult.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                (new LangStr
                {
                    ["en"] = "Invalid login attempt.",
                    ["et"] = "Vigane sisselogimise katse."
                }).Translate() ?? "Invalid login attempt.");
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

        var companiesResult = await _identityService.GetUserCompaniesAsync(userId.Value);
        if (companiesResult.Success && companiesResult.Data != null && companiesResult.Data.Companies.Count > 0)
        {
            if (companiesResult.Data.Companies.Count == 1)
            {
                var company = companiesResult.Data.Companies[0];
                return RedirectToCompanyHome(company.CompanySlug, company.Role);
            }

            return RedirectToAction(nameof(CompanySelection), new { returnUrl = model.ReturnUrl });
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

        var companiesResult = await _identityService.GetUserCompaniesAsync(userId.Value);
        if (!companiesResult.Success || companiesResult.Data == null || companiesResult.Data.Companies.Count == 0)
        {
            await _identityService.LogoutAsync();
            return RedirectToAction(nameof(Login));
        }

        // If user has only one company, redirect directly
        if (companiesResult.Data.Companies.Count == 1)
        {
            var company = companiesResult.Data.Companies.First();
            return RedirectToCompanyHome(company.CompanySlug, company.Role);
        }

        var viewModel = new CompanySelectionViewModel
        {
            ReturnUrl = returnUrl,
            Companies = companiesResult.Data.Companies.Select(c => new CompanySelectionItemViewModel
            {
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName,
                CompanySlug = c.CompanySlug,
                Role = c.Role
            }).ToList(),
            SelectedCompanyId = companiesResult.Data.Companies.First().CompanyId
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
        var companiesResult = await _identityService.GetUserCompaniesAsync(userId.Value);
        if (!companiesResult.Success || companiesResult.Data == null)
        {
            await _identityService.LogoutAsync();
            return RedirectToAction(nameof(Login));
        }

        var selectedCompanyId = model.SelectedCompanyId;
        var company = companiesResult.Data.Companies.FirstOrDefault(c => c.CompanyId == selectedCompanyId);
        if (company == null || !ModelState.IsValid)
        {
            // Reload the model with companies and show error
            model.Companies = companiesResult.Data.Companies.Select(c => new CompanySelectionItemViewModel
            {
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName,
                CompanySlug = c.CompanySlug,
                Role = c.Role
            }).ToList();
            
            ModelState.AddModelError(string.Empty, "Invalid company selection.");
            return View(model);
        }

        var activeCompany = await _identityService.SetActiveCompanyAsync(
            userId.Value,
            company.CompanyId,
            User.Identity?.Name ?? userId.Value.ToString());

        if (!activeCompany.Success || activeCompany.Data == null)
        {
            return Forbid();
        }

        var companySlugs = companiesResult.Data.Companies
            .Select(c => c.CompanySlug)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalizedReturnUrl = NormalizeCompanyReturnUrl(
            model.ReturnUrl,
            activeCompany.Data.CompanySlug,
            companySlugs);

        if (!string.IsNullOrWhiteSpace(normalizedReturnUrl) && Url.IsLocalUrl(normalizedReturnUrl))
        {
            return Redirect(normalizedReturnUrl);
        }

        return RedirectToCompanyHome(activeCompany.Data.CompanySlug, activeCompany.Data.Role);
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

        var activeCompany = await _identityService.SetActiveCompanyAsync(
            userId.Value,
            companyId,
            User.Identity?.Name ?? userId.Value.ToString());

        if (!activeCompany.Success || activeCompany.Data == null)
        {
            return Forbid();
        }

        var companiesResult = await _identityService.GetUserCompaniesAsync(userId.Value);
        var companySlugs = companiesResult.Success && companiesResult.Data != null
            ? companiesResult.Data.Companies
                .Select(c => c.CompanySlug)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var normalizedReturnUrl = NormalizeCompanyReturnUrl(
            returnUrl,
            activeCompany.Data.CompanySlug,
            companySlugs);

        if (!string.IsNullOrWhiteSpace(normalizedReturnUrl) && Url.IsLocalUrl(normalizedReturnUrl))
        {
            return Redirect(normalizedReturnUrl);
        }

        return RedirectToCompanyHome(activeCompany.Data.CompanySlug, activeCompany.Data.Role);
    }

    // POST: /Account/Logout
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _identityService.LogoutAsync();
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
        var isEmployee = string.Equals(role, ECompanyRole.Employee.ToString(), StringComparison.OrdinalIgnoreCase);
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
}
