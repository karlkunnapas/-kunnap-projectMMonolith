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
    public class UserPromotionsController : Controller
    {
        private readonly AppDbContext _context;

        public UserPromotionsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/UserPromotions
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.UserPromotions.Include(u => u.Promotion).Include(u => u.User);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Admin/UserPromotions/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userPromotion = await _context.UserPromotions
                .Include(u => u.Promotion)
                .Include(u => u.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (userPromotion == null)
            {
                return NotFound();
            }

            return View(userPromotion);
        }

        // GET: Admin/UserPromotions/Create
        public IActionResult Create()
        {
            ViewData["PromotionId"] = new SelectList(_context.Promotions, "Id", "Code");
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: Admin/UserPromotions/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("UserId,PromotionId,AddedAt,IsUsed,Id")] UserPromotion userPromotion)
        {
            if (ModelState.IsValid)
            {
                userPromotion.Id = Guid.NewGuid();
                _context.Add(userPromotion);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["PromotionId"] = new SelectList(_context.Promotions, "Id", "Code", userPromotion.PromotionId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", userPromotion.UserId);
            return View(userPromotion);
        }

        // GET: Admin/UserPromotions/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userPromotion = await _context.UserPromotions.FindAsync(id);
            if (userPromotion == null)
            {
                return NotFound();
            }
            ViewData["PromotionId"] = new SelectList(_context.Promotions, "Id", "Code", userPromotion.PromotionId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", userPromotion.UserId);
            return View(userPromotion);
        }

        // POST: Admin/UserPromotions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("UserId,PromotionId,AddedAt,IsUsed,Id")] UserPromotion userPromotion)
        {
            if (id != userPromotion.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(userPromotion);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserPromotionExists(userPromotion.Id))
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
            ViewData["PromotionId"] = new SelectList(_context.Promotions, "Id", "Code", userPromotion.PromotionId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", userPromotion.UserId);
            return View(userPromotion);
        }

        // GET: Admin/UserPromotions/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userPromotion = await _context.UserPromotions
                .Include(u => u.Promotion)
                .Include(u => u.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (userPromotion == null)
            {
                return NotFound();
            }

            return View(userPromotion);
        }

        // POST: Admin/UserPromotions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var userPromotion = await _context.UserPromotions.FindAsync(id);
            if (userPromotion != null)
            {
                _context.UserPromotions.Remove(userPromotion);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UserPromotionExists(Guid id)
        {
            return _context.UserPromotions.Any(e => e.Id == id);
        }
    }
}
