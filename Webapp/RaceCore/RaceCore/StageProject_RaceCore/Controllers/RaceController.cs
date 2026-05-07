using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    /* RaceController.cs
       Purpose: Manage races, stages and race entries. Provides CRUD
       operations for Race objects and handles dynamic stage creation
       during race creation. Basic error handling via TempData messages.
    */
    /// <summary>
    /// Controller for creating, editing and listing races.
    /// </summary>
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
                    .Include(r => r.Stages)
                    .Include(r => r.RaceEntries)
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
            return View(new Race());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Race race)
        {
            if (!ModelState.IsValid)
            {
                return View(race);
            }

            try
            {
                // 1. Race opslaan
                _context.Races.Add(race);
                await _context.SaveChangesAsync();

                // 2. Nieuwe renner (optioneel)
                var newCyclistName = Request.Form["NewCyclistName"];

                if (!string.IsNullOrWhiteSpace(newCyclistName))
                {
                    var cyclist = new Cyclist
                    {
                        FirstName = newCyclistName
                    };

                    _context.Cyclists.Add(cyclist);
                    await _context.SaveChangesAsync();

                    // automatisch toevoegen aan race
                    _context.RaceEntries.Add(new RaceEntry
                    {
                        RaceId = race.Id,
                        CyclistId = cyclist.Id
                    });
                }
                 
                // 3. Geselecteerde renners
                var selectedCyclists = Request.Form["SelectedCyclistIds"];

                foreach (var cyclistId in selectedCyclists)
                {
                    _context.RaceEntries.Add(new RaceEntry
                    {
                        RaceId = race.Id,
                        CyclistId = int.Parse(cyclistId)
                    });
                }

                // 4. Stages (dynamisch)
                int i = 0;
                while (!string.IsNullOrEmpty(Request.Form[$"Stages[{i}].Name"]))
                {
                    var stage = new Stage
                    {
                        Name = Request.Form[$"Stages[{i}].Name"],
                        Date = DateTime.Parse(Request.Form[$"Stages[{i}].Date"]),
                        RaceId = race.Id
                    };

                    _context.Stages.Add(stage);
                    i++;
                }

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["Error"] = "Database fout.";
                return View(race);
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var race = await _context.Races.FindAsync(id);
                if (race == null) return NotFound();

                return View(race);
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Year,StartDate,EndDate")] Race race)
        {
            if (id != race.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                return View(race);
            }

            try
            {
                _context.Races.Update(race);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
                return View(race);
            }
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var race = await _context.Races
                    .Include(r => r.Stages)
                    .Include(r => r.RaceEntries)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (race == null) return NotFound();

                return View(race);
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            try
            {
                var race = await _context.Races.FirstOrDefaultAsync(r => r.Id == id);
                if (race == null) return NotFound();

                return View(race);
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
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
