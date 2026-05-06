using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class ScoringController : Controller
    {
        private readonly AppDbContext _context;

        public ScoringController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? raceId, int? stageId)
        {
            var viewModel = new ScoringViewModel();

            try
            {
                var races = await _context.Races
                    .OrderBy(r => r.Id)
                    .ToListAsync();

                if (!races.Any())
                {
                    TempData["Error"] = "Geen wedstrijden gevonden.";
                    return View(viewModel);
                }

                // --- BEPAAL RACE ---
                int selectedRaceId = raceId
                    ?? (stageId.HasValue
                        ? await _context.Stages
                            .Where(s => s.Id == stageId.Value)
                            .Select(s => s.RaceId)
                            .FirstOrDefaultAsync()
                        : races.First().Id);

                // fallback als stage niet bestaat
                if (selectedRaceId == 0)
                    selectedRaceId = races.First().Id;

                // --- STAGES ---
                var stages = await _context.Stages
                    .Where(s => s.RaceId == selectedRaceId)
                    .OrderBy(s => s.StageNumber)
                    .ToListAsync();

                if (!stages.Any())
                {
                    TempData["Error"] = "Geen ritten gevonden.";
                    ViewBag.Races = races;
                    ViewBag.SelectedRaceId = selectedRaceId;
                    ViewBag.AvailableStages = new List<SelectListItem>();
                    return View(viewModel);
                }

                // --- BEPAAL STAGE ---
                int selectedStageId = stageId.HasValue && stages.Any(s => s.Id == stageId.Value)
                    ? stageId.Value
                    : stages.First().Id;

                viewModel.StageId = selectedStageId;

                // --- CYCLISTEN ---
                viewModel.AvailableCyclists = await _context.Cyclists
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.FirstName + " " + c.LastName
                    })
                    .ToListAsync();

                // --- RESULTATEN ---
                var results = await _context.StageResults
                    .Where(r => r.StageId == selectedStageId)
                    .Include(r => r.Cyclist)
                    .OrderBy(r => r.Position)
                    .ToListAsync();

                var jerseys = await _context.Jerseys
                    .Where(j => j.StageId == selectedStageId)
                    .ToListAsync();

                // --- TOP 25 ---
                for (int i = 1; i <= 25; i++)
                {
                    var r = results.FirstOrDefault(x => x.Position == i);

                    viewModel.Results.Add(new StageResultViewModel
                    {
                        Position = i,
                        CyclistId = r?.CyclistId,
                        CyclistName = r?.Cyclist?.FullName ?? "",
                        HasYellowJersey = HasJersey(jerseys, r?.CyclistId, "Red"),
                        HasGreenJersey = HasJersey(jerseys, r?.CyclistId, "Green"),
                        HasPolkaJersey = HasJersey(jerseys, r?.CyclistId, "Blue"),
                        HasWhiteJersey = HasJersey(jerseys, r?.CyclistId, "White")
                    });
                }

                // --- BUITEN TOP 25 ---
                var top25Ids = results.Select(r => r.CyclistId).ToHashSet();

                SetOutsideJersey(viewModel, jerseys, top25Ids, "Red");
                SetOutsideJersey(viewModel, jerseys, top25Ids, "Green");
                SetOutsideJersey(viewModel, jerseys, top25Ids, "Blue");
                SetOutsideJersey(viewModel, jerseys, top25Ids, "White");

                // --- VIEWBAG ---
                ViewBag.Races = races;
                ViewBag.SelectedRaceId = selectedRaceId;
                ViewBag.AvailableStages = stages.Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"Rit {s.StageNumber} - {s.Name}",
                    Selected = s.Id == selectedStageId
                }).ToList();
            }
            catch
            {
                TempData["Error"] = "Database fout.";
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveScores(ScoringViewModel model, int raceId)
        {
            try
            {
                if (model.StageId <= 0)
                    return RedirectToAction("Index");

                var stage = await _context.Stages.FindAsync(model.StageId);
                if (stage == null)
                {
                    TempData["Error"] = "Rit niet gevonden.";
                    return RedirectToAction("Index");
                }

                // --- DUPLICATES CHECK ---
                var ids = model.Results
                    .Where(r => r.CyclistId.HasValue)
                    .Select(r => r.CyclistId.Value)
                    .ToList();

                if (model.YellowOutsideTop25CyclistId.HasValue) ids.Add(model.YellowOutsideTop25CyclistId.Value);
                if (model.GreenOutsideTop25CyclistId.HasValue) ids.Add(model.GreenOutsideTop25CyclistId.Value);
                if (model.PolkaOutsideTop25CyclistId.HasValue) ids.Add(model.PolkaOutsideTop25CyclistId.Value);
                if (model.WhiteOutsideTop25CyclistId.HasValue) ids.Add(model.WhiteOutsideTop25CyclistId.Value);

                var duplicate = ids.GroupBy(x => x).FirstOrDefault(g => g.Count() > 1);

                if (duplicate != null)
                {
                    var rider = await _context.Cyclists.FindAsync(duplicate.Key);
                    TempData["Error"] = $"Renner '{rider?.FullName}' staat dubbel.";
                    return RedirectToAction("Index", new { stageId = model.StageId, raceId });
                }

                // --- DELETE OUDE DATA ---
                var oldResults = _context.StageResults.Where(r => r.StageId == model.StageId);
                var oldJerseys = _context.Jerseys.Where(j => j.StageId == model.StageId);

                _context.StageResults.RemoveRange(oldResults);
                _context.Jerseys.RemoveRange(oldJerseys);
                await _context.SaveChangesAsync();

                var used = new HashSet<string>();

                // --- SAVE TOP 25 ---
                foreach (var r in model.Results.OrderBy(x => x.Position))
                {
                    if (!r.CyclistId.HasValue) continue;

                    _context.StageResults.Add(new StageResult
                    {
                        StageId = model.StageId,
                        CyclistId = r.CyclistId.Value,
                        Position = r.Position,
                        Status = "Finished"
                    });

                    if (r.HasYellowJersey) AddJersey(model.StageId, r.CyclistId.Value, "Red", used);
                    if (r.HasGreenJersey) AddJersey(model.StageId, r.CyclistId.Value, "Green", used);
                    if (r.HasPolkaJersey) AddJersey(model.StageId, r.CyclistId.Value, "Blue", used);
                    if (r.HasWhiteJersey) AddJersey(model.StageId, r.CyclistId.Value, "White", used);
                }

                // --- BUITEN TOP 25 ---
                AddOutside(model.StageId, model.YellowOutsideTop25CyclistId, "Red", used);
                AddOutside(model.StageId, model.GreenOutsideTop25CyclistId, "Green", used);
                AddOutside(model.StageId, model.PolkaOutsideTop25CyclistId, "Blue", used);
                AddOutside(model.StageId, model.WhiteOutsideTop25CyclistId, "White", used);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Opgeslagen!";
                return RedirectToAction("Index", new { stageId = model.StageId, raceId });
            }
            catch
            {
                TempData["Error"] = "Opslaan mislukt.";
                return RedirectToAction("Index", new { stageId = model.StageId, raceId });
            }
        }

        // --- HELPERS ---

        private bool HasJersey(List<Jersey> list, int? cyclistId, string type)
        {
            if (!cyclistId.HasValue) return false;
            return list.Any(j => j.CyclistId == cyclistId && j.Type == type);
        }

        private void SetOutsideJersey(ScoringViewModel vm, List<Jersey> jerseys, HashSet<int> top25, string type)
        {
            var j = jerseys.FirstOrDefault(x => x.Type == type && !top25.Contains(x.CyclistId));
            if (j == null) return;

            if (type == "Red") vm.YellowOutsideTop25CyclistId = j.CyclistId;
            if (type == "Green") vm.GreenOutsideTop25CyclistId = j.CyclistId;
            if (type == "Blue") vm.PolkaOutsideTop25CyclistId = j.CyclistId;
            if (type == "White") vm.WhiteOutsideTop25CyclistId = j.CyclistId;
        }

        private void AddOutside(int stageId, int? cyclistId, string type, HashSet<string> used)
        {
            if (!cyclistId.HasValue) return;
            AddJersey(stageId, cyclistId.Value, type, used);
        }

        private void AddJersey(int stageId, int cyclistId, string type, HashSet<string> used)
        {
            if (used.Contains(type)) return;

            _context.Jerseys.Add(new Jersey
            {
                StageId = stageId,
                CyclistId = cyclistId,
                Type = type
            });

            used.Add(type);
        }
    }
}