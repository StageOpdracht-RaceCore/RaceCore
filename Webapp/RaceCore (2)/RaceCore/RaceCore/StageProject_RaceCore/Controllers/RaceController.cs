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
                ModelState.AddModelError(nameof(model.SelectedCyclistIds), "Select at least 1 cyclist.");
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

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var race = await _context.Races
                .Include(r => r.Stages)
                .Include(r => r.RaceEntries)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (race == null) return NotFound();

            var model = new RaceEditViewModel
            {
                Id = race.Id,
                Name = race.Name,
                Year = race.Year,
                StartDate = race.StartDate,
                EndDate = race.EndDate,

                SelectedCyclistIds = race.RaceEntries
                    .Select(re => re.CyclistId)
                    .ToList(),

                Stages = race.Stages
                    .OrderBy(s => s.StageNumber)
                    .Select(s => new RaceStageInputViewModel
                    {
                        Name = s.Name,
                        Date = s.Date
                    })
                    .ToList(),

                AvailableCyclists = await GetAvailableCyclists()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(RaceEditViewModel model)
        {
            if (model.EndDate != null && model.StartDate != null && model.EndDate < model.StartDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "Einddatum mag niet vroeger zijn dan begindatum.");
            }

            if (!model.SelectedCyclistIds.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedCyclistIds), "Select at least 1 cyclist.");
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

            var race = await _context.Races
                .Include(r => r.Stages)
                .Include(r => r.RaceEntries)
                .FirstOrDefaultAsync(r => r.Id == model.Id);

            if (race == null) return NotFound();

            race.Name = model.Name.Trim();
            race.Year = model.Year;
            race.StartDate = model.StartDate;
            race.EndDate = model.EndDate;

            var selectedCyclistIds = model.SelectedCyclistIds
                .Distinct()
                .ToList();

            var existingEntries = race.RaceEntries.ToList();

            var entriesToRemove = existingEntries
                .Where(re => !selectedCyclistIds.Contains(re.CyclistId))
                .ToList();

            _context.RaceEntries.RemoveRange(entriesToRemove);

            var existingCyclistIds = existingEntries
                .Select(re => re.CyclistId)
                .ToList();

            var cyclistIdsToAdd = selectedCyclistIds
                .Where(cyclistId => !existingCyclistIds.Contains(cyclistId))
                .ToList();

            foreach (var cyclistId in cyclistIdsToAdd)
            {
                _context.RaceEntries.Add(new RaceEntry
                {
                    RaceId = race.Id,
                    CyclistId = cyclistId,
                    Status = "Active"
                });
            }

            _context.Stages.RemoveRange(race.Stages);

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

            TempData["Success"] = "Race succesvol aangepast.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var race = await _context.Races
                .Include(r => r.Stages)
                .Include(r => r.RaceEntries)
                    .ThenInclude(re => re.Cyclist)
                        .ThenInclude(c => c.Team)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (race == null) return NotFound();

            return View(race);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var race = await _context.Races
                .Include(r => r.Stages)
                .Include(r => r.RaceEntries)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (race == null) return NotFound();

            return View(race);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var race = await _context.Races
                .Include(r => r.Stages)
                .Include(r => r.RaceEntries)
                .Include(r => r.PlayerSelections)
                .Include(r => r.DraftTurns)
                .Include(r => r.PlayerPoints)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (race == null)
            {
                TempData["Error"] = "Race niet gevonden.";
                return RedirectToAction(nameof(Index));
            }

            var stageIds = race.Stages
                .Select(s => s.Id)
                .ToList();

            var stageResults = await _context.StageResults
                .Where(sr => stageIds.Contains(sr.StageId))
                .ToListAsync();

            var jerseys = await _context.Jerseys
                .Where(j => stageIds.Contains(j.StageId))
                .ToListAsync();

            var stagePlayerPoints = await _context.PlayerPoints
                .Where(pp => pp.StageId != null && stageIds.Contains(pp.StageId.Value))
                .ToListAsync();

            var gameSessions = await _context.GameSessions
                .Where(gs => gs.RaceId == id)
                .ToListAsync();

            _context.Jerseys.RemoveRange(jerseys);
            _context.StageResults.RemoveRange(stageResults);
            _context.PlayerPoints.RemoveRange(stagePlayerPoints);
            _context.PlayerPoints.RemoveRange(race.PlayerPoints);
            _context.PlayerSelections.RemoveRange(race.PlayerSelections);
            _context.DraftTurns.RemoveRange(race.DraftTurns);
            _context.GameSessions.RemoveRange(gameSessions);
            _context.RaceEntries.RemoveRange(race.RaceEntries);
            _context.Stages.RemoveRange(race.Stages);
            _context.Races.Remove(race);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Race en alle gekoppelde gegevens zijn succesvol verwijderd.";
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