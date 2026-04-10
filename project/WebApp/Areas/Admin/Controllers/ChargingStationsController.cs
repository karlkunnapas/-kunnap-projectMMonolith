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
    public class ChargingStationsController : Controller
    {
        private readonly AppDbContext _context;

        public ChargingStationsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/ChargingStations
        public async Task<IActionResult> Index()
        {
            var stations = await _context.ChargingStations
                .Include(c => c.Company)
                .ToListAsync();

            return View(stations.Select(MapToViewModel).ToList());
        }

        // GET: Admin/ChargingStations/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chargingStation = await _context.ChargingStations
                .Include(c => c.Company)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (chargingStation == null)
            {
                return NotFound();
            }

            return View(MapToViewModel(chargingStation));
        }

        // GET: Admin/ChargingStations/Create
        public IActionResult Create()
        {
            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "ContactEmail");
            ViewData["StatusOptions"] = BuildStatusSelectList(EStationStatus.Available);
            return View(new ChargingStationAdminViewModel());
        }

        // POST: Admin/ChargingStations/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ChargingStationAdminViewModel model)
        {
            var postedStatus = ResolvePostedStatus(model.Status, Request.Form["Status"]);

            if (!IsValidStationStatus(postedStatus))
            {
                ModelState.AddModelError(nameof(model.Status), "Invalid station status.");
            }

            if (ModelState.IsValid)
            {
                var normalizedStatus = NormalizeStationStatus(postedStatus);
                var chargingStation = new ChargingStation
                {
                    Id = Guid.NewGuid(),
                    Location = model.Location,
                    Status = normalizedStatus,
                    PricePerKwh = model.PricePerKwh,
                    MaxPower = model.MaxPower,
                    IsActive = model.IsActive,
                    CompanyId = model.CompanyId,
                    Name = new LangStr()
                };

                chargingStation.Name.SetTranslation(model.NameEt, "et");
                chargingStation.Name.SetTranslation(model.NameEn, "en");
                _context.Add(chargingStation);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "ContactEmail", model.CompanyId);
            ViewData["StatusOptions"] = BuildStatusSelectList(NormalizeStationStatus(model.Status));
            return View(model);
        }

        // GET: Admin/ChargingStations/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chargingStation = await _context.ChargingStations
                .Include(c => c.Company)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (chargingStation == null)
            {
                return NotFound();
            }

            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "ContactEmail", chargingStation.CompanyId);
            var model = MapToViewModel(chargingStation);
            ViewData["StatusOptions"] = BuildStatusSelectList(model.Status);
            return View(model);
        }

        // POST: Admin/ChargingStations/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, ChargingStationAdminViewModel model)
        {
            if (id == Guid.Empty)
            {
                id = model.Id;
            }

            if (id != model.Id)
            {
                return NotFound();
            }

            var postedStatus = ResolvePostedStatus(model.Status, Request.Form["Status"]);

            if (!IsValidStationStatus(postedStatus))
            {
                ModelState.AddModelError(nameof(model.Status), "Invalid station status.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var chargingStation = await _context.ChargingStations.FindAsync(id);
                    if (chargingStation == null)
                    {
                        return NotFound();
                    }

                    chargingStation.Location = model.Location;
                    chargingStation.Status = NormalizeStationStatus(postedStatus);
                    chargingStation.PricePerKwh = model.PricePerKwh;
                    chargingStation.MaxPower = model.MaxPower;
                    chargingStation.IsActive = model.IsActive;
                    chargingStation.CompanyId = model.CompanyId;
                    chargingStation.Name ??= new LangStr();
                    chargingStation.Name.SetTranslation(model.NameEt, "et");
                    chargingStation.Name.SetTranslation(model.NameEn, "en");

                    // LangStr is a mutable JSON-backed value; force EF to persist Name edits.
                    _context.Entry(chargingStation).Property(x => x.Name).IsModified = true;
                    _context.Entry(chargingStation).Property(x => x.Location).IsModified = true;
                    _context.Entry(chargingStation).Property(x => x.Status).IsModified = true;
                    _context.Entry(chargingStation).Property(x => x.PricePerKwh).IsModified = true;
                    _context.Entry(chargingStation).Property(x => x.MaxPower).IsModified = true;
                    _context.Entry(chargingStation).Property(x => x.IsActive).IsModified = true;
                    _context.Entry(chargingStation).Property(x => x.CompanyId).IsModified = true;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ChargingStationExists(model.Id))
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

            ViewData["CompanyId"] = new SelectList(_context.Companies, "Id", "ContactEmail", model.CompanyId);
            ViewData["StatusOptions"] = BuildStatusSelectList(NormalizeStationStatus(postedStatus));
            return View(model);
        }

        // GET: Admin/ChargingStations/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var chargingStation = await _context.ChargingStations
                .Include(c => c.Company)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (chargingStation == null)
            {
                return NotFound();
            }

            return View(MapToViewModel(chargingStation));
        }

        // POST: Admin/ChargingStations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var chargingStation = await _context.ChargingStations.FindAsync(id);
            if (chargingStation != null)
            {
                _context.ChargingStations.Remove(chargingStation);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ChargingStationExists(Guid id)
        {
            return _context.ChargingStations.Any(e => e.Id == id);
        }

        private static ChargingStationAdminViewModel MapToViewModel(ChargingStation station)
        {
            return new ChargingStationAdminViewModel
            {
                Id = station.Id,
                Name = station.Name?.Translate() ?? string.Empty,
                NameEt = station.Name?.Translate("et") ?? string.Empty,
                NameEn = station.Name?.Translate("en") ?? string.Empty,
                Location = station.Location,
                Status = NormalizeStationStatus(station.Status),
                PricePerKwh = station.PricePerKwh,
                MaxPower = station.MaxPower,
                IsActive = station.IsActive,
                CompanyId = station.CompanyId,
                CompanyContactEmail = station.Company?.ContactEmail ?? string.Empty
            };
        }

        private static EStationStatus NormalizeStationStatus(EStationStatus status)
        {
            var rawValue = (int)status;

            // Legacy status value 1 (Reserved) was removed; treat it as InUse.
            if (rawValue == 1)
            {
                return EStationStatus.InUse;
            }

            return Enum.IsDefined(typeof(EStationStatus), status)
                ? status
                : EStationStatus.Available;
        }

        private static bool IsValidStationStatus(EStationStatus status)
        {
            var rawValue = (int)status;
            return rawValue == 1 || Enum.IsDefined(typeof(EStationStatus), status);
        }

        private static EStationStatus ResolvePostedStatus(EStationStatus fallback, string? rawStatus)
        {
            if (string.IsNullOrWhiteSpace(rawStatus))
            {
                return fallback;
            }

            if (int.TryParse(rawStatus, out var statusInt))
            {
                return (EStationStatus)statusInt;
            }

            return Enum.TryParse<EStationStatus>(rawStatus, true, out var parsed)
                ? parsed
                : fallback;
        }

        private static SelectList BuildStatusSelectList(EStationStatus selected)
        {
            var values = Enum.GetValues<EStationStatus>()
                .Select(value => new
                {
                    Value = ((int)value).ToString(),
                    Text = value.ToString()
                })
                .ToList();

            return new SelectList(values, "Value", "Text", ((int)NormalizeStationStatus(selected)).ToString());
        }
    }
}
