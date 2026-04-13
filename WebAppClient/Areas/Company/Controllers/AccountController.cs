using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.ViewModels.Account;
using WebAppClient.Models;
using WebAppClient.Services;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IApiClient _apiClient;

    public AccountController(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var token = await _apiClient.PostAsync<JwtResponseDto>(
                "api/v1/customeraccount/register-company",
                new RegisterCompanyRequestDto
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    Password = model.Password,
                    ConfirmPassword = model.ConfirmPassword,
                    CompanyName = model.CompanyName,
                    CompanySlug = model.CompanySlug
                });

            await SignInFromJwtAsync(token.JWT, token.RefreshToken);
            HttpContext.Session.SetString("SelectedCompanySlug", model.CompanySlug);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard", new { area = "Company", companySlug = model.CompanySlug });
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    private async Task SignInFromJwtAsync(string jwt, string refreshToken)
    {
        _apiClient.SaveTokens(jwt, refreshToken);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        var identity = new ClaimsIdentity(token.Claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
