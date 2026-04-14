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
    public class VehicleConnectorsController : Controller
    {
        private readonly AppDbContext _context;

        public VehicleConnectorsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/VehicleConnectors
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.VehicleConnectors.Include(v => v.Connector).Include(v => v.Vehicle);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Admin/VehicleConnectors/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicleConnector = await _context.VehicleConnectors
                .Include(v => v.Connector)
                .Include(v => v.Vehicle)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (vehicleConnector == null)
            {
                return NotFound();
            }

            return View(vehicleConnector);
        }

        // GET: Admin/VehicleConnectors/Create
        public IActionResult Create()
        {
            ViewData["ConnectorId"] = new SelectList(_context.Connectors, "Id", "Id");
            ViewData["VehicleId"] = new SelectList(_context.Vehicles, "Id", "Make");
            return View();
        }

        // POST: Admin/VehicleConnectors/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("VehicleId,ConnectorId,Id")] VehicleConnector vehicleConnector)
        {
            if (ModelState.IsValid)
            {
                vehicleConnector.Id = Guid.NewGuid();
                _context.Add(vehicleConnector);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ConnectorId"] = new SelectList(_context.Connectors, "Id", "Id", vehicleConnector.ConnectorId);
            ViewData["VehicleId"] = new SelectList(_context.Vehicles, "Id", "Make", vehicleConnector.VehicleId);
            return View(vehicleConnector);
        }

        // GET: Admin/VehicleConnectors/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicleConnector = await _context.VehicleConnectors.FindAsync(id);
            if (vehicleConnector == null)
            {
                return NotFound();
            }
            ViewData["ConnectorId"] = new SelectList(_context.Connectors, "Id", "Id", vehicleConnector.ConnectorId);
            ViewData["VehicleId"] = new SelectList(_context.Vehicles, "Id", "Make", vehicleConnector.VehicleId);
            return View(vehicleConnector);
        }

        // POST: Admin/VehicleConnectors/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("VehicleId,ConnectorId,Id")] VehicleConnector vehicleConnector)
        {
            if (id != vehicleConnector.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(vehicleConnector);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VehicleConnectorExists(vehicleConnector.Id))
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
            ViewData["ConnectorId"] = new SelectList(_context.Connectors, "Id", "Id", vehicleConnector.ConnectorId);
            ViewData["VehicleId"] = new SelectList(_context.Vehicles, "Id", "Make", vehicleConnector.VehicleId);
            return View(vehicleConnector);
        }

        // GET: Admin/VehicleConnectors/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicleConnector = await _context.VehicleConnectors
                .Include(v => v.Connector)
                .Include(v => v.Vehicle)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (vehicleConnector == null)
            {
                return NotFound();
            }

            return View(vehicleConnector);
        }

        // POST: Admin/VehicleConnectors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var vehicleConnector = await _context.VehicleConnectors.FindAsync(id);
            if (vehicleConnector != null)
            {
                _context.VehicleConnectors.Remove(vehicleConnector);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VehicleConnectorExists(Guid id)
        {
            return _context.VehicleConnectors.Any(e => e.Id == id);
        }
    }
}
