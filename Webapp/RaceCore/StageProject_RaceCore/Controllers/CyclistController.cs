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

        public async Task<IActionResult> Index(string? searchString, string? activeFilter)
        {
            var query = _context.Cyclists
                .Include(c => c.Team)
                .AsQueryable();

            // 🔍 Zoeken
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(c =>
                    c.FirstName.Contains(searchString) ||
                    c.LastName.Contains(searchString) ||
                    (c.Team != null && c.Team.Name.Contains(searchString))
                );
            }

            // 🔘 Filter IsActive
            if (!string.IsNullOrWhiteSpace(activeFilter))
            {
                switch (activeFilter.ToLower())
                {
                    case "yes":
                        query = query.Where(c => c.IsActive);
                        break;

                    case "no":
                        query = query.Where(c => !c.IsActive);
                        break;
                }
            }

            ViewBag.SearchString = searchString;
            ViewBag.ActiveFilter = activeFilter;

            var cyclists = await query
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToListAsync();

            return View(cyclists);
        }
    }
}
