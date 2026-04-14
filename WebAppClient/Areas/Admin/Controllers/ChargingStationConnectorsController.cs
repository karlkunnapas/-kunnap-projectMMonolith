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
    public class ChargingStationConnectorsController : Controller
    {
        private readonly AppDbContext _context;

        public ChargingStationConnectorsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/ChargingStationConnectors
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.ChargingStationConnectors.Include(c => c.ChargingStation).Include(c => c.Connector);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Admin/ChargingStationConnectors/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chargingStationConnector = await _context.ChargingStationConnectors
                .Include(c => c.ChargingStation)
                .Include(c => c.Connector)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (chargingStationConnector == null)
            {
                return NotFound();
            }

            return View(chargingStationConnector);
        }

        // GET: Admin/ChargingStationConnectors/Create
        public IActionResult Create()
        {
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location");
            ViewData["ConnectorId"] = new SelectList(_context.Connectors, "Id", "Id");
            return View();
        }

        // POST: Admin/ChargingStationConnectors/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ChargingStationId,ConnectorId,Id")] ChargingStationConnector chargingStationConnector)
        {
            if (ModelState.IsValid)
            {
                chargingStationConnector.Id = Guid.NewGuid();
                _context.Add(chargingStationConnector);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location", chargingStationConnector.ChargingStationId);
            ViewData["ConnectorId"] = new SelectList(_context.Connectors, "Id", "Id", chargingStationConnector.ConnectorId);
            return View(chargingStationConnector);
        }

        // GET: Admin/ChargingStationConnectors/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chargingStationConnector = await _context.ChargingStationConnectors.FindAsync(id);
            if (chargingStationConnector == null)
            {
                return NotFound();
            }
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location", chargingStationConnector.ChargingStationId);
            ViewData["ConnectorId"] = new SelectList(_context.Connectors, "Id", "Id", chargingStationConnector.ConnectorId);
            return View(chargingStationConnector);
        }

        // POST: Admin/ChargingStationConnectors/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("ChargingStationId,ConnectorId,Id")] ChargingStationConnector chargingStationConnector)
        {
            if (id != chargingStationConnector.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(chargingStationConnector);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ChargingStationConnectorExists(chargingStationConnector.Id))
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
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location", chargingStationConnector.ChargingStationId);
            ViewData["ConnectorId"] = new SelectList(_context.Connectors, "Id", "Id", chargingStationConnector.ConnectorId);
            return View(chargingStationConnector);
        }

        // GET: Admin/ChargingStationConnectors/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chargingStationConnector = await _context.ChargingStationConnectors
                .Include(c => c.ChargingStation)
                .Include(c => c.Connector)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (chargingStationConnector == null)
            {
                return NotFound();
            }

            return View(chargingStationConnector);
        }

        // POST: Admin/ChargingStationConnectors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var chargingStationConnector = await _context.ChargingStationConnectors.FindAsync(id);
            if (chargingStationConnector != null)
            {
                _context.ChargingStationConnectors.Remove(chargingStationConnector);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ChargingStationConnectorExists(Guid id)
        {
            return _context.ChargingStationConnectors.Any(e => e.Id == id);
        }
    }
}
