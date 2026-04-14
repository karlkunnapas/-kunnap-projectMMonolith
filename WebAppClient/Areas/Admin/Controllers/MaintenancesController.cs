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
    public class MaintenancesController : Controller
    {
        private readonly AppDbContext _context;

        public MaintenancesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Maintenances
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.Maintenances.Include(m => m.AssignedToUser).Include(m => m.ChargingStation).Include(m => m.ReportedByUser);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Admin/Maintenances/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var maintenance = await _context.Maintenances
                .Include(m => m.AssignedToUser)
                .Include(m => m.ChargingStation)
                .Include(m => m.ReportedByUser)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (maintenance == null)
            {
                return NotFound();
            }

            return View(maintenance);
        }

        // GET: Admin/Maintenances/Create
        public IActionResult Create()
        {
            ViewData["AssignedToUserId"] = new SelectList(_context.Users, "Id", "Id");
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location");
            ViewData["ReportedByUserId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: Admin/Maintenances/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ChargingStationId,ReportedByUserId,IssueDescription,Status,ReportedAt,ResolvedAt,AssignedToUserId,Notes,Id")] Maintenance maintenance)
        {
            if (ModelState.IsValid)
            {
                maintenance.Id = Guid.NewGuid();
                _context.Add(maintenance);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AssignedToUserId"] = new SelectList(_context.Users, "Id", "Id", maintenance.AssignedToUserId);
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location", maintenance.ChargingStationId);
            ViewData["ReportedByUserId"] = new SelectList(_context.Users, "Id", "Id", maintenance.ReportedByUserId);
            return View(maintenance);
        }

        // GET: Admin/Maintenances/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var maintenance = await _context.Maintenances.FindAsync(id);
            if (maintenance == null)
            {
                return NotFound();
            }
            ViewData["AssignedToUserId"] = new SelectList(_context.Users, "Id", "Id", maintenance.AssignedToUserId);
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location", maintenance.ChargingStationId);
            ViewData["ReportedByUserId"] = new SelectList(_context.Users, "Id", "Id", maintenance.ReportedByUserId);
            return View(maintenance);
        }

        // POST: Admin/Maintenances/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("ChargingStationId,ReportedByUserId,IssueDescription,Status,ReportedAt,ResolvedAt,AssignedToUserId,Notes,Id")] Maintenance maintenance)
        {
            if (id != maintenance.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(maintenance);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MaintenanceExists(maintenance.Id))
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
            ViewData["AssignedToUserId"] = new SelectList(_context.Users, "Id", "Id", maintenance.AssignedToUserId);
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location", maintenance.ChargingStationId);
            ViewData["ReportedByUserId"] = new SelectList(_context.Users, "Id", "Id", maintenance.ReportedByUserId);
            return View(maintenance);
        }

        // GET: Admin/Maintenances/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var maintenance = await _context.Maintenances
                .Include(m => m.AssignedToUser)
                .Include(m => m.ChargingStation)
                .Include(m => m.ReportedByUser)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (maintenance == null)
            {
                return NotFound();
            }

            return View(maintenance);
        }

        // POST: Admin/Maintenances/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var maintenance = await _context.Maintenances.FindAsync(id);
            if (maintenance != null)
            {
                _context.Maintenances.Remove(maintenance);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MaintenanceExists(Guid id)
        {
            return _context.Maintenances.Any(e => e.Id == id);
        }
    }
}
