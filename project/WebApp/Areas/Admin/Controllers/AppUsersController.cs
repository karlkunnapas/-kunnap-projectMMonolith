using App.DAL.EF;
using App.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,root")]
public class AppUsersController : Controller
{
    private readonly AppDbContext _context;

    public AppUsersController(AppDbContext context)
    {
        _context = context;
    }

    // GET: Admin/AppUsers
    public async Task<IActionResult> Index()
    {
        return View(await _context.Users.ToListAsync());
    }

    // GET: Admin/AppUsers/Details/5
    public async Task<IActionResult> Details(Guid? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var appUser = await _context.Users
            .FirstOrDefaultAsync(m => m.Id == id);
        if (appUser == null)
        {
            return NotFound();
        }

        return View(appUser);
    }

    // GET: Admin/AppUsers/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Admin/AppUsers/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumber,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnd,LockoutEnabled,AccessFailedCount,Id")] AppUser appUser)
    {
        if (ModelState.IsValid)
        {
            appUser.Id = Guid.NewGuid();
            appUser.SecurityStamp ??= Guid.NewGuid().ToString("N");
            appUser.ConcurrencyStamp ??= Guid.NewGuid().ToString("N");
            _context.Add(appUser);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return View(appUser);
    }

    // GET: Admin/AppUsers/Edit/5
    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var appUser = await _context.Users.FindAsync(id);
        if (appUser == null)
        {
            return NotFound();
        }
        return View(appUser);
    }

    // POST: Admin/AppUsers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumber,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnd,LockoutEnabled,AccessFailedCount,Id")] AppUser appUser)
    {
        if (id != appUser.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(appUser);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AppUserExists(appUser.Id))
                {
                    return NotFound();
                }

                throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(appUser);
    }

    // GET: Admin/AppUsers/Delete/5
    public async Task<IActionResult> Delete(Guid? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var appUser = await _context.Users
            .FirstOrDefaultAsync(m => m.Id == id);
        if (appUser == null)
        {
            return NotFound();
        }

        return View(appUser);
    }

    // POST: Admin/AppUsers/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var appUser = await _context.Users.FindAsync(id);
        if (appUser != null)
        {
            _context.Users.Remove(appUser);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool AppUserExists(Guid id)
    {
        return _context.Users.Any(e => e.Id == id);
    }
}
