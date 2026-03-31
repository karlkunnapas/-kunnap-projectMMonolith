using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using App.DAL.EF;
using App.Domain;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,root")]
    public class AppUserCompaniesController : Controller
    {
        private readonly AppDbContext _context;

        public AppUserCompaniesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/AppUserCompanies
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.AppUserCompanies.Include(a => a.AppUser).Include(a => a.Company);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Admin/AppUserCompanies/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appUserCompany = await _context.AppUserCompanies
                .Include(a => a.AppUser)
                .Include(a => a.Company)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (appUserCompany == null)
            {
                return NotFound();
            }

            return View(appUserCompany);
        }

        // GET: Admin/AppUserCompanies/Create
        public IActionResult Create()
        {
            ViewData["AppUserId"] = new SelectList(_context.Users, "Id", "Id");
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "ContactEmail");
            return View();
        }

        // POST: Admin/AppUserCompanies/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AppUserId,CompanyId,Role,IsActive,JoinedAtUtc,Id")] AppUserCompany appUserCompany)
        {
            if (ModelState.IsValid)
            {
                appUserCompany.Id = Guid.NewGuid();
                _context.Add(appUserCompany);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AppUserId"] = new SelectList(_context.Users, "Id", "Id", appUserCompany.AppUserId);
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "ContactEmail", appUserCompany.CompanyId);
            return View(appUserCompany);
        }

        // GET: Admin/AppUserCompanies/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appUserCompany = await _context.AppUserCompanies.FindAsync(id);
            if (appUserCompany == null)
            {
                return NotFound();
            }
            ViewData["AppUserId"] = new SelectList(_context.Users, "Id", "Id", appUserCompany.AppUserId);
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "ContactEmail", appUserCompany.CompanyId);
            return View(appUserCompany);
        }

        // POST: Admin/AppUserCompanies/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("AppUserId,CompanyId,Role,IsActive,JoinedAtUtc,Id")] AppUserCompany appUserCompany)
        {
            if (id != appUserCompany.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(appUserCompany);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AppUserCompanyExists(appUserCompany.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["AppUserId"] = new SelectList(_context.Users, "Id", "Id", appUserCompany.AppUserId);
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "ContactEmail", appUserCompany.CompanyId);
            return View(appUserCompany);
        }

        // GET: Admin/AppUserCompanies/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appUserCompany = await _context.AppUserCompanies
                .Include(a => a.AppUser)
                .Include(a => a.Company)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (appUserCompany == null)
            {
                return NotFound();
            }

            return View(appUserCompany);
        }

        // POST: Admin/AppUserCompanies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var appUserCompany = await _context.AppUserCompanies.FindAsync(id);
            if (appUserCompany != null)
            {
                _context.AppUserCompanies.Remove(appUserCompany);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AppUserCompanyExists(Guid id)
        {
            return _context.AppUserCompanies.Any(e => e.Id == id);
        }
    }
}
