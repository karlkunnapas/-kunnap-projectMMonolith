using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Modules.Users.Domain;
using Shared.Contracts.Companies;
using Shared.Contracts.Users;
using WebApp.ViewModels.Account;

namespace WebApp.Areas.Company.Controllers;

[Area("Company")]
[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IUsersModuleApi _usersModuleApi;
    private readonly ICompaniesModuleApi _companiesModuleApi;
    private readonly SignInManager<AppUser> _signInManager;

    public AccountController(
        IUsersModuleApi usersModuleApi,
        ICompaniesModuleApi companiesModuleApi,
        SignInManager<AppUser> signInManager)
    {
        _usersModuleApi = usersModuleApi;
        _companiesModuleApi = companiesModuleApi;
        _signInManager = signInManager;
    }

    // GET: /Company/Account/Register
    public IActionResult Register(string? returnUrl = null)
    {
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    // POST: /Company/Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        model.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var registration = await _usersModuleApi.RegisterCustomerAsync(new RegisterCustomerContract
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            Password = model.Password
        });

        if (!registration.Success)
        {
            ModelState.AddModelError(string.Empty, registration.ErrorMessage ?? "Unable to register user.");
            return View(model);
        }

        var companyCreation = await _companiesModuleApi.CreateCompanyWithOwnerMembershipAsync(new CreateCompanyWithOwnerMembershipContract
        {
            OwnerUserId = registration.UserId,
            ContactEmail = model.Email,
            ContactPhone = model.PhoneNumber ?? string.Empty,
            CompanyName = model.CompanyName,
            CompanySlug = model.CompanySlug
        });

        if (!companyCreation.Success)
        {
            ModelState.AddModelError(string.Empty, companyCreation.ErrorMessage ?? "Unable to create company.");
            return View(model);
        }

        var signInResult = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);
        if (!signInResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, App.Resources.Views.Account.Register.ResourceManager.GetString("AutoLoginFailed") ?? "Registration completed but automatic login failed. Please log in manually.");
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard", new { area = "Company" });
    }
}
