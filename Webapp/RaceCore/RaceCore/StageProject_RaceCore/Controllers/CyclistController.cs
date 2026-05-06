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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string firstName, string lastName, int? teamId)
        {
            if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
            {
                var cyclist = new Cyclist
                {
                    FirstName = firstName.Trim(),
                    LastName = lastName.Trim(),
                    TeamId = teamId == 0 ? null : teamId,
                    IsActive = true
                };

                _context.Cyclists.Add(cyclist);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"{cyclist.FullName} is succesvol toegevoegd.";
            }
            else
            {
                TempData["CreateError"] = "Voornaam en achternaam zijn verplicht.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cyclist = await _context.Cyclists.FindAsync(id);
            if (cyclist != null)
            {
                _context.Cyclists.Remove(cyclist);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{cyclist.FullName} is verwijderd.";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Index(string? search, string? status, int page = 1, int pageSize = 25)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = 1;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.CyclistCount = 0;
            ViewBag.Races = new List<Race>();
            ViewBag.Teams = new List<Team>();
            ViewBag.DatabaseOnline = false;

            try
            {
                var races = await _context.Races
                    .Where(r =>
                    r.Name.Contains("Giro") ||
                    r.Name.Contains("Tour") ||
                    r.Name.Contains("Vuelta"))
                    .ToListAsync();

                var teams = await _context.Teams.OrderBy(t => t.Name).ToListAsync();

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

                ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize));
                ViewBag.CyclistCount = totalItems;
                ViewBag.Races = races;
                ViewBag.Teams = teams;
                ViewBag.DatabaseOnline = true;

                return View(cyclists);
            }
            catch
            {
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om live gegevens te zien.";
                return View(new List<Cyclist>());
            }
        }
    }
}
