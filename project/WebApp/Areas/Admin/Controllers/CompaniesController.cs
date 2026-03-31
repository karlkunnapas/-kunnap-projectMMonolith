using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using App.DAL.EF;
using App.Domain;
using WebApp.Areas.Admin.ViewModels;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,root")]
    public class CompaniesController : Controller
    {
        private readonly AppDbContext _context;

        public CompaniesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Companies
        public async Task<IActionResult> Index()
        {
            var companies = await _context.Companies.ToListAsync();
            return View(companies.Select(MapToViewModel).ToList());
        }

        // GET: Admin/Companies/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var company = await _context.Companies.FirstOrDefaultAsync(m => m.Id == id);
            if (company == null)
            {
                return NotFound();
            }

            return View(MapToViewModel(company));
        }

        // GET: Admin/Companies/Create
        public IActionResult Create()
        {
            return View(new CompanyAdminViewModel());
        }

        // POST: Admin/Companies/Create
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CompanyAdminViewModel model)
        {
            if (ModelState.IsValid)
            {
                var company = new App.Domain.Company
                {
                    Id = Guid.NewGuid(),
                    ContactEmail = model.ContactEmail,
                    ContactPhone = model.ContactPhone,
                    Slug = model.Slug,
                    IsActive = model.IsActive,
                    Name = new LangStr()
                };
                company.Name.SetTranslation(model.Name);
                _context.Add(company);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: Admin/Companies/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var company = await _context.Companies.FindAsync(id);
            if (company == null)
            {
                return NotFound();
            }

            return View(MapToViewModel(company));
        }

        // POST: Admin/Companies/Edit/5
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, CompanyAdminViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var company = await _context.Companies.FindAsync(id);
                    if (company == null)
                    {
                        return NotFound();
                    }

                    company.ContactEmail = model.ContactEmail;
                    company.ContactPhone = model.ContactPhone;
                    company.Slug = model.Slug;
                    company.IsActive = model.IsActive;
                    company.Name ??= new LangStr();
                    company.Name.SetTranslation(model.Name);
                    _context.Entry(company).Property(x => x.Name).IsModified = true;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CompanyExists(model.Id))
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

            return View(model);
        }

        // GET: Admin/Companies/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var company = await _context.Companies.FirstOrDefaultAsync(m => m.Id == id);
            if (company == null)
            {
                return NotFound();
            }

            return View(MapToViewModel(company));
        }

        // POST: Admin/Companies/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var company = await _context.Companies.FindAsync(id);
            if (company != null)
            {
                _context.Companies.Remove(company);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CompanyExists(Guid id)
        {
            return _context.Companies.Any(e => e.Id == id);
        }

        private static CompanyAdminViewModel MapToViewModel(App.Domain.Company company)
        {
            return new CompanyAdminViewModel
            {
                Id = company.Id,
                Name = company.Name?.Translate() ?? string.Empty,
                ContactEmail = company.ContactEmail,
                ContactPhone = company.ContactPhone,
                Slug = company.Slug,
                IsActive = company.IsActive
            };
        }
    }
}
