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
    public class ChargingSessionsController : Controller
    {
        private readonly AppDbContext _context;

        public ChargingSessionsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/ChargingSessions
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.ChargingSessions
                .Include(c => c.ChargingStation)
                .Include(c => c.Reservation)
                .Include(c => c.User)
                .Include(c => c.Promotion);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Admin/ChargingSessions/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chargingSession = await _context.ChargingSessions
                .Include(c => c.ChargingStation)
                .Include(c => c.Reservation)
                .Include(c => c.User)
                .Include(c => c.Promotion)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (chargingSession == null)
            {
                return NotFound();
            }

            return View(chargingSession);
        }

        // GET: Admin/ChargingSessions/Create
        public IActionResult Create()
        {
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location");
            ViewData["PromotionId"] = new SelectList(_context.Promotions, "Id", "Code");
            ViewData["ReservationId"] = new SelectList(_context.Reservations, "Id", "Id");
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: Admin/ChargingSessions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("UserId,ChargingStationId,ReservationId,PromotionId,StartTime,EndTime,EnergyConsumed,Cost,Id")] ChargingSession chargingSession)
        {
            if (ModelState.IsValid)
            {
                chargingSession.Id = Guid.NewGuid();
                _context.Add(chargingSession);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location", chargingSession.ChargingStationId);
            ViewData["PromotionId"] = new SelectList(_context.Promotions, "Id", "Code", chargingSession.PromotionId);
            ViewData["ReservationId"] = new SelectList(_context.Reservations, "Id", "Id", chargingSession.ReservationId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", chargingSession.UserId);
            return View(chargingSession);
        }

        // GET: Admin/ChargingSessions/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chargingSession = await _context.ChargingSessions.FindAsync(id);
            if (chargingSession == null)
            {
                return NotFound();
            }
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location", chargingSession.ChargingStationId);
            ViewData["PromotionId"] = new SelectList(_context.Promotions, "Id", "Code", chargingSession.PromotionId);
            ViewData["ReservationId"] = new SelectList(_context.Reservations, "Id", "Id", chargingSession.ReservationId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", chargingSession.UserId);
            return View(chargingSession);
        }

        // POST: Admin/ChargingSessions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("UserId,ChargingStationId,ReservationId,PromotionId,StartTime,EndTime,EnergyConsumed,Cost,Id")] ChargingSession chargingSession)
        {
            if (id != chargingSession.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(chargingSession);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ChargingSessionExists(chargingSession.Id))
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
            ViewData["ChargingStationId"] = new SelectList(_context.ChargingStations, "Id", "Location", chargingSession.ChargingStationId);
            ViewData["PromotionId"] = new SelectList(_context.Promotions, "Id", "Code", chargingSession.PromotionId);
            ViewData["ReservationId"] = new SelectList(_context.Reservations, "Id", "Id", chargingSession.ReservationId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", chargingSession.UserId);
            return View(chargingSession);
        }

        // GET: Admin/ChargingSessions/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chargingSession = await _context.ChargingSessions
                .Include(c => c.ChargingStation)
                .Include(c => c.Reservation)
                .Include(c => c.User)
                .Include(c => c.Promotion)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (chargingSession == null)
            {
                return NotFound();
            }

            return View(chargingSession);
        }

        // POST: Admin/ChargingSessions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var chargingSession = await _context.ChargingSessions.FindAsync(id);
            if (chargingSession != null)
            {
                _context.ChargingSessions.Remove(chargingSession);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ChargingSessionExists(Guid id)
        {
            return _context.ChargingSessions.Any(e => e.Id == id);
        }
    }
}
