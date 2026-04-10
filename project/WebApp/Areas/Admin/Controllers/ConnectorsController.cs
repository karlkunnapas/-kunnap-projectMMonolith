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
    public class ConnectorsController : Controller
    {
        private readonly AppDbContext _context;

        public ConnectorsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Connectors
        public async Task<IActionResult> Index()
        {
            var connectors = await _context.Connectors.ToListAsync();
            return View(connectors.Select(MapToViewModel).ToList());
        }

        // GET: Admin/Connectors/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var connector = await _context.Connectors.FirstOrDefaultAsync(m => m.Id == id);
            if (connector == null)
            {
                return NotFound();
            }

            return View(MapToViewModel(connector));
        }

        // GET: Admin/Connectors/Create
        public IActionResult Create()
        {
            return View(new ConnectorAdminViewModel());
        }

        // POST: Admin/Connectors/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ConnectorAdminViewModel model)
        {
            if (ModelState.IsValid)
            {
                var connector = new Connector
                {
                    Id = Guid.NewGuid(),
                    IsActive = model.IsActive,
                    Name = new LangStr()
                };
                connector.Name.SetTranslation(model.NameEt, "et");
                connector.Name.SetTranslation(model.NameEn, "en");
                _context.Add(connector);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: Admin/Connectors/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var connector = await _context.Connectors.FindAsync(id);
            if (connector == null)
            {
                return NotFound();
            }

            return View(MapToViewModel(connector));
        }

        // POST: Admin/Connectors/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, ConnectorAdminViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var connector = await _context.Connectors.FindAsync(id);
                    if (connector == null)
                    {
                        return NotFound();
                    }

                    connector.IsActive = model.IsActive;
                    connector.Name ??= new LangStr();
                    connector.Name.SetTranslation(model.NameEt, "et");
                    connector.Name.SetTranslation(model.NameEn, "en");
                    _context.Entry(connector).Property(x => x.Name).IsModified = true;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ConnectorExists(model.Id))
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

        // GET: Admin/Connectors/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var connector = await _context.Connectors.FirstOrDefaultAsync(m => m.Id == id);
            if (connector == null)
            {
                return NotFound();
            }

            return View(MapToViewModel(connector));
        }

        // POST: Admin/Connectors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var connector = await _context.Connectors.FindAsync(id);
            if (connector != null)
            {
                _context.Connectors.Remove(connector);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ConnectorExists(Guid id)
        {
            return _context.Connectors.Any(e => e.Id == id);
        }

        private static ConnectorAdminViewModel MapToViewModel(Connector connector)
        {
            return new ConnectorAdminViewModel
            {
                Id = connector.Id,
                Name = connector.Name?.Translate() ?? string.Empty,
                NameEt = connector.Name?.Translate("et") ?? string.Empty,
                NameEn = connector.Name?.Translate("en") ?? string.Empty,
                IsActive = connector.IsActive
            };
        }
    }
}
