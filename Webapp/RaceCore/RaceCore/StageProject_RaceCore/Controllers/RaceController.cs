using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class RaceController : Controller
    {
        private readonly AppDbContext _context;

        public RaceController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var races = await _context.Races
                    .OrderByDescending(r => r.Year)
                    .ThenBy(r => r.Name)
                    .ToListAsync();

                ViewBag.DatabaseOnline = true;
                return View(races);
            }
            catch
            {
                ViewBag.DatabaseOnline = false;
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om live races te zien.";
                return View(new List<Race>());
            }
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Year,StartDate,EndDate")] Race race)
        {
            if (!ModelState.IsValid) return View(race);

            try
            {
                _context.Add(race);
                await _context.SaveChangesAsync();
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
                return View(race);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return RedirectToAction(nameof(Index));

            try
            {
                var race = await _context.Races.FindAsync(id);
                if (race == null) return RedirectToAction(nameof(Index));
                return View(race);
            }
            catch
            {
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om te bewerken.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Year,StartDate,EndDate")] Race race)
        {
            if (id != race.Id) return RedirectToAction(nameof(Index));
            if (!ModelState.IsValid) return View(race);

            try
            {
                _context.Update(race);
                await _context.SaveChangesAsync();
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
                return View(race);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return RedirectToAction(nameof(Index));

            try
            {
                var race = await _context.Races
                    .Include(r => r.Stages)
                    .Include(r => r.RaceEntries)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (race == null) return RedirectToAction(nameof(Index));
                return View(race);
            }
            catch
            {
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om details te zien.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return RedirectToAction(nameof(Index));

            try
            {
                var race = await _context.Races.FirstOrDefaultAsync(m => m.Id == id);
                if (race == null) return RedirectToAction(nameof(Index));
                return View(race);
            }
            catch
            {
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om te verwijderen.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var race = await _context.Races.FindAsync(id);
                if (race != null)
                {
                    _context.Races.Remove(race);
                    await _context.SaveChangesAsync();
                }
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
