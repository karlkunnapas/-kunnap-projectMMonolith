using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels.Account;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Controllers;

public class AccountController : Controller
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IApiClient apiClient, ILogger<AccountController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["CompanyRegisterUrl"] = Url.Action("Register", "Account", new { area = "Company" });
        return View(new CustomerRegisterViewModel());
    }

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

        try
        {
            var token = await _apiClient.PostAsync<JwtResponseDto>(
                "api/v1/customeraccount/register-customer",
                new RegisterCustomerRequestDto
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    Password = model.Password,
                    ConfirmPassword = model.ConfirmPassword
                });

            await SignInFromJwtAsync(token.JWT, token.RefreshToken, false);
            return await RedirectAfterAuthAsync(returnUrl);
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var token = await _apiClient.PostAsync<JwtResponseDto>(
                "api/v1/account/login",
                new LoginRequestDto
                {
                    Email = model.Email,
                    Password = model.Password
                });

            await SignInFromJwtAsync(token.JWT, token.RefreshToken, model.RememberMe);
            return await RedirectAfterAuthAsync(model.ReturnUrl);
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> CompanySelection(string? returnUrl = null)
    {
        try
        {
            var companies = await _apiClient.GetAsync<UserCompaniesResponseDto>("api/v1/customeraccount/companies");
            if (companies.Companies.Count == 0)
            {
                return RedirectToAction(nameof(LoggedOut));
            }

            if (companies.Companies.Count == 1)
            {
                SetSelectedCompany(companies.Companies[0]);
                return RedirectToCompanyHome(companies.Companies[0].CompanySlug, companies.Companies[0].Role);
            }

            return View(new CompanySelectionViewModel
            {
                ReturnUrl = returnUrl,
                SelectedCompanyId = companies.Companies[0].CompanyId,
                Companies = companies.Companies
                    .Select(c => new CompanySelectionItemViewModel
                    {
                        CompanyId = c.CompanyId,
                        CompanyName = c.CompanyName,
                        CompanySlug = c.CompanySlug,
                        Role = c.Role
                    })
                    .ToList()
            });
        }
        catch (ApiException)
        {
            await LogoutInternalAsync();
            return RedirectToAction(nameof(Login));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompanySelection(CompanySelectionViewModel model)
    {
        try
        {
            var companies = await _apiClient.GetAsync<UserCompaniesResponseDto>("api/v1/customeraccount/companies");
            var selected = companies.Companies.FirstOrDefault(c => c.CompanyId == model.SelectedCompanyId);
            if (!ModelState.IsValid || selected == null)
            {
                model.Companies = companies.Companies
                    .Select(c => new CompanySelectionItemViewModel
                    {
                        CompanyId = c.CompanyId,
                        CompanyName = c.CompanyName,
                        CompanySlug = c.CompanySlug,
                        Role = c.Role
                    })
                    .ToList();
                ModelState.AddModelError(string.Empty, "Invalid company selection.");
                return View(model);
            }

            SetSelectedCompany(selected);
            var normalizedReturnUrl = NormalizeCompanyReturnUrl(model.ReturnUrl, selected.CompanySlug, companies.Companies.Select(c => c.CompanySlug).ToHashSet(StringComparer.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(normalizedReturnUrl) && Url.IsLocalUrl(normalizedReturnUrl))
            {
                return Redirect(normalizedReturnUrl);
            }

            return RedirectToCompanyHome(selected.CompanySlug, selected.Role);
        }
        catch (ApiException)
        {
            await LogoutInternalAsync();
            return RedirectToAction(nameof(Login));
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchCompany(Guid companyId, string? returnUrl = null)
    {
        try
        {
            var companies = await _apiClient.GetAsync<UserCompaniesResponseDto>("api/v1/customeraccount/companies");
            var selected = companies.Companies.FirstOrDefault(c => c.CompanyId == companyId);
            if (selected == null)
            {
                return Forbid();
            }

            SetSelectedCompany(selected);
            var normalizedReturnUrl = NormalizeCompanyReturnUrl(returnUrl, selected.CompanySlug, companies.Companies.Select(c => c.CompanySlug).ToHashSet(StringComparer.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(normalizedReturnUrl) && Url.IsLocalUrl(normalizedReturnUrl))
            {
                return Redirect(normalizedReturnUrl);
            }

            return RedirectToCompanyHome(selected.CompanySlug, selected.Role);
        }
        catch (ApiException)
        {
            return Forbid();
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await LogoutInternalAsync();
        return RedirectToAction(nameof(LoggedOut));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult LoggedOut()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task SignInFromJwtAsync(string jwt, string refreshToken, bool isPersistent)
    {
        _apiClient.SaveTokens(jwt, refreshToken);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        var claims = token.Claims.ToList();

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = isPersistent
        });
    }

    private async Task<IActionResult> RedirectAfterAuthAsync(string? returnUrl)
    {
        try
        {
            var companies = await _apiClient.GetAsync<UserCompaniesResponseDto>("api/v1/customeraccount/companies");
            if (companies.Companies.Count == 1)
            {
                SetSelectedCompany(companies.Companies[0]);
                return RedirectToCompanyHome(companies.Companies[0].CompanySlug, companies.Companies[0].Role);
            }

            if (companies.Companies.Count > 1)
            {
                return RedirectToAction(nameof(CompanySelection), new { returnUrl });
            }
        }
        catch (ApiException ex)
        {
            _logger.LogInformation(ex, "User has no company memberships or company lookup failed after login.");
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    private void SetSelectedCompany(UserCompanyItemDto company)
    {
        HttpContext.Session.SetString("SelectedCompanyId", company.CompanyId.ToString());
        HttpContext.Session.SetString("SelectedCompanySlug", company.CompanySlug);
        HttpContext.Session.SetString("SelectedCompanyRole", company.Role);
    }

    private IActionResult RedirectToCompanyHome(string companySlug, string? role)
    {
        var isEmployee = string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase);
        return isEmployee
            ? RedirectToAction("Index", "Maintenance", new { area = "Company", companySlug })
            : RedirectToAction("Index", "Dashboard", new { area = "Company", companySlug });
    }

    private async Task LogoutInternalAsync()
    {
        try
        {
            var refreshToken = _apiClient.GetRefreshToken();
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await _apiClient.PostAsync("api/v1/account/logout", new LogoutRequestDto
                {
                    RefreshToken = refreshToken
                });
            }
        }
        catch (ApiException)
        {
        }

        _apiClient.ClearTokens();
        HttpContext.Session.Remove("SelectedCompanyId");
        HttpContext.Session.Remove("SelectedCompanySlug");
        HttpContext.Session.Remove("SelectedCompanyRole");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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
        if (segments.Length == 0 || !knownCompanySlugs.Contains(segments[0]))
        {
            return returnUrl;
        }

        segments[0] = targetCompanySlug;
        return "/" + string.Join('/', segments) + queryPart + hashPart;
    }
}
