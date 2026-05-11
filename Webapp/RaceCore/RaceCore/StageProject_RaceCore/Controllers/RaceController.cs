using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

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

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new RaceCreateViewModel
            {
                Year = DateTime.Now.Year,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today,
                AvailableCyclists = await GetAvailableCyclists()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RaceCreateViewModel model)
        {
            if (model.EndDate != null && model.StartDate != null && model.EndDate < model.StartDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "Einddatum mag niet vroeger zijn dan begindatum.");
            }

            if (!model.SelectedCyclistIds.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedCyclistIds), "Selecteer minstens 1 wielrenner.");
            }

            var validStages = model.Stages
                .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                .ToList();

            if (!validStages.Any())
            {
                ModelState.AddModelError(nameof(model.Stages), "Voeg minstens 1 stage toe.");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableCyclists = await GetAvailableCyclists();
                return View(model);
            }

            var race = new Race
            {
                Name = model.Name.Trim(),
                Year = model.Year,
                StartDate = model.StartDate,
                EndDate = model.EndDate
            };

            _context.Races.Add(race);
            await _context.SaveChangesAsync();

            foreach (var cyclistId in model.SelectedCyclistIds.Distinct())
            {
                _context.RaceEntries.Add(new RaceEntry
                {
                    RaceId = race.Id,
                    CyclistId = cyclistId,
                    Status = "Active"
                });
            }

            int stageNumber = 1;

            foreach (var stage in validStages)
            {
                _context.Stages.Add(new Stage
                {
                    RaceId = race.Id,
                    StageNumber = stageNumber++,
                    Name = stage.Name.Trim(),
                    Date = stage.Date
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Race succesvol aangemaakt.";
            return RedirectToAction(nameof(Index));
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
                    .ThenInclude(re => re.Cyclist)
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
        [ValidateAntiForgeryToken]
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

        private async Task<List<Cyclist>> GetAvailableCyclists()
        {
            return await _context.Cyclists
                .Include(c => c.Team)
                .Where(c => c.IsActive)
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToListAsync();
        }
    }
}