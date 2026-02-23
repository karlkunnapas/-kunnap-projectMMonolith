using System;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using App.DAL.EF;
using App.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApp.Areas.Root.ViewModels;

namespace WebApp.Areas.Root.Controllers;

[Area("Root")]
[Authorize(Roles = "root")]
public class UsersController : Controller
{
    private readonly ILogger<UsersController> _logger;
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;


    public UsersController(ILogger<UsersController> logger, AppDbContext context, UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Index()
    {
        var res = await _userManager.Users.OrderBy(u => u.Email).ToListAsync();
        return View(res);
    }

    public async Task<IActionResult> RoleRemove(Guid userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return RedirectToAction("Index", new { error = "User Not Found" });
        }

        var result = await _userManager.RemoveFromRoleAsync(user, role);
        if (result.Succeeded)
        {
            return RedirectToAction("Index");
        }

        return RedirectToAction("Index", new { error = result.Errors.Select(e => e.Description).First() });
    }

    public async Task<IActionResult> RoleAdd(Guid userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return RedirectToAction("Index", new { error = "User Not Found" });
        }

        var result = await _userManager.AddToRoleAsync(user, role);
        if (result.Succeeded)
        {
            return RedirectToAction("Index");
        }

        return RedirectToAction("Index", new { error = result.Errors.Select(e => e.Description).First() });
    }

    public async Task<IActionResult> PasswordLink(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return RedirectToAction("Index", new { error = "User Not Found" });
        }
        
        var code = await _userManager.GeneratePasswordResetTokenAsync(user);
        
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page(
            "/Account/ResetPassword",
            pageHandler: null,
            values: new { area = "Identity", code },
            protocol: Request.Scheme);


        var url = HtmlEncoder.Default.Encode(callbackUrl!);

        var vm = new PasswordLinkViewModel()
        {
            AppUser = user,
            PasswordLink = url,
        };

        return View(vm);
    }

}