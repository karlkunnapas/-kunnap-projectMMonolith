using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.DAL.EF;
using App.Domain;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,root")]
    public class AuditLogsController : Controller
    {
        private readonly AppDbContext _context;

        public AuditLogsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Admin/AuditLogs
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.AuditLogs.Include(a => a.Company);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Admin/AuditLogs/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var auditLog = await _context.AuditLogs
                .Include(a => a.Company)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (auditLog == null)
            {
                return NotFound();
            }

            return View(auditLog);
        }
    }
}
