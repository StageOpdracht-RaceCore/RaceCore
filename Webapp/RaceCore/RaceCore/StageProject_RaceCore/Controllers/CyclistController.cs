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

        public async Task<IActionResult> Index(string? search, bool? active, int page = 1, int pageSize = 25)
        {
            var query = _context.Cyclists
                .Include(c => c.Team)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c =>
                    c.FirstName.Contains(search) ||
                    c.LastName.Contains(search) ||
                    c.Team.Name.Contains(search));
            }

            if (active.HasValue)
            {
                query = query.Where(c => c.IsActive == active.Value);
            }

            var totalItems = await query.CountAsync();

            var cyclists = await query
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;

            return View(cyclists);
        }

    }
}
