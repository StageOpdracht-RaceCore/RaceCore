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
            var races = await _context.Races
                .Include(r => r.Stages)
                .Include(r => r.RaceEntries)
                .OrderByDescending(r => r.Year)
                .ThenBy(r => r.Name)
                .ToListAsync();

            return View(races);
        }

        public IActionResult Create()
        {
            ViewBag.Cyclists = _context.Cyclists
                .Where(c => c.IsActive)
                .OrderBy(c => c.LastName)
                .ToList();

            return View(new Race
            {
                Year = DateTime.Now.Year
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Race race, int[] selectedCyclists)
        {
            // Safety check
            if (!ModelState.IsValid)
            {
                ViewBag.Cyclists = _context.Cyclists.ToList();
                return View(race);
            }

            // Save parent first
            _context.Races.Add(race);
            await _context.SaveChangesAsync();

            // ------------------------
            // CYCLISTS
            // ------------------------
            if (selectedCyclists != null && selectedCyclists.Length > 0)
            {
                foreach (var id in selectedCyclists)
                {
                    _context.RaceEntries.Add(new RaceEntry
                    {
                        RaceId = race.Id,
                        CyclistId = id,
                        Status = "Active"
                    });
                }
            }

            // ------------------------
            // STAGES (ROBUST FIX)
            // ------------------------
            if (race.Stages != null && race.Stages.Count > 0)
            {
                int stageNr = 1;

                foreach (var stage in race.Stages)
                {
                    if (stage == null || string.IsNullOrWhiteSpace(stage.Name))
                        continue;

                    _context.Stages.Add(new Stage
                    {
                        RaceId = race.Id,
                        StageNumber = stageNr++,
                        Name = stage.Name,
                        Date = stage.Date
                    });
                }
            }

            // Save children
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var race = await _context.Races.FindAsync(id);
            if (race == null) return NotFound();

            return View(race);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Race race)
        {
            if (!ModelState.IsValid)
                return View(race);

            _context.Races.Update(race);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var race = await _context.Races
                .Include(r => r.Stages)
                .Include(r => r.RaceEntries)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (race == null) return NotFound();

            return View(race);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var race = await _context.Races.FirstOrDefaultAsync(r => r.Id == id);
            if (race == null) return NotFound();

            return View(race);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var race = await _context.Races.FindAsync(id);

            if (race != null)
            {
                _context.Races.Remove(race);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}