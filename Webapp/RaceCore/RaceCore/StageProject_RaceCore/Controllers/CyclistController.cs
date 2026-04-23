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

        public async Task<IActionResult> Index(string? search, string? status, int page = 1, int pageSize = 25)
        {
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 25;
            }

            var races = await _context.Races
                .OrderBy(r => r.StartDate)
                .ThenBy(r => r.Name)
                .Take(3)
                .ToListAsync();

            var query = _context.Cyclists
                .Include(c => c.Team)
                .Include(c => c.RaceEntries)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(c =>
                    c.FirstName.Contains(search) ||
                    c.LastName.Contains(search) ||
                    (c.Team != null && c.Team.Name.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "active")
                {
                    query = query.Where(c => c.IsActive);
                }
                else if (status == "inactive")
                {
                    query = query.Where(c => !c.IsActive);
                }
                else if (status.StartsWith("race-") && int.TryParse(status.Replace("race-", ""), out int raceId))
                {
                    query = query.Where(c => c.RaceEntries.Any(re => re.RaceId == raceId));
                }
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
            ViewBag.Status = status;
            ViewBag.CyclistCount = totalItems;
            ViewBag.Races = races;

            return View(cyclists);
        }
    }
}