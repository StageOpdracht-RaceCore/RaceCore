using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class CyclistController : Controller
    {
        private readonly AppDbContext _context;

        public CyclistController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, string? active)
        {
            var query = _context.Cyclists
                .Include(c => c.Team)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c =>
                    c.FirstName.ToLower().Contains(search) ||
                    c.LastName.ToLower().Contains(search) ||
                    c.Team.Name.ToLower().Contains(search)
                );
            }
            if (!string.IsNullOrEmpty(active)) { 
                bool isActive = active.ToLower() == "true";
                query = query.Where(c => c.IsActive == isActive);
            }

            var cyclistList = await query.ToListAsync();

            ViewBag.CyclistCount = cyclistList.Count;

            return View(cyclistList);
        }
    }
}
