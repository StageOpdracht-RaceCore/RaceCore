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

        public async Task<IActionResult> Index(int? raceId = null, int? stageId = null)
        {
            var viewModel = new ScoringViewModel();

            try
            {
                var races = await _context.Races
                    .OrderBy(r => r.Id)
                    .ToListAsync();

                if (!races.Any())
                {
                    TempData["Error"] = "Geen wedstrijden gevonden in de database.";
                    return View(viewModel);
                }

                var selectedRaceId = raceId ?? races.First().Id;

                if (stageId.HasValue)
                {
                    var selectedStage = await _context.Stages
                        .FirstOrDefaultAsync(s => s.Id == stageId.Value);

                    if (selectedStage != null)
                        selectedRaceId = selectedStage.RaceId;
                }

                var stages = await _context.Stages
                    .Where(s => s.RaceId == selectedRaceId)
                    .OrderBy(s => s.StageNumber)
                    .ToListAsync();

                if (!stages.Any())
                {
                    TempData["Error"] = "Geen ritten gevonden voor deze wedstrijd.";
                    ViewBag.Races = races;
                    ViewBag.SelectedRaceId = selectedRaceId;
                    ViewBag.AvailableStages = new List<SelectListItem>();
                    return View(viewModel);
                }

                var selectedStageId = stageId.HasValue && stages.Any(s => s.Id == stageId.Value)
                    ? stageId.Value
                    : stages.First().Id;

                viewModel.StageId = selectedStageId;

                viewModel.AvailableCyclists = await _context.Cyclists
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.FirstName + " " + c.LastName
                    })
                    .ToListAsync();

                var existingResults = await _context.StageResults
                    .Where(r => r.StageId == selectedStageId)
                    .Include(r => r.Cyclist)
                    .OrderBy(r => r.Position)
                    .ToListAsync();

                var existingJerseys = await _context.Jerseys
                    .Where(j => j.StageId == selectedStageId)
                    .Include(j => j.Cyclist)
                    .ToListAsync();

                for (int i = 1; i <= 25; i++)
                {
                    var result = existingResults.FirstOrDefault(r => r.Position == i);

                    viewModel.Results.Add(new StageResultViewModel
                    {
                        Position = i,
                        CyclistId = result?.CyclistId,
                        CyclistName = result?.Cyclist?.FullName ?? "",

                        // ViewModel-namen blijven hetzelfde,
                        // maar database-types zijn nu Red, Green, Blue, White.
                        HasYellowJersey = result != null && HasJersey(existingJerseys, result.CyclistId, "Red"),
                        HasGreenJersey = result != null && HasJersey(existingJerseys, result.CyclistId, "Green"),
                        HasPolkaJersey = result != null && HasJersey(existingJerseys, result.CyclistId, "Blue"),
                        HasWhiteJersey = result != null && HasJersey(existingJerseys, result.CyclistId, "White")
                    });
                }

                var top25Ids = existingResults
                    .Select(r => r.CyclistId)
                    .ToHashSet();

                SetOutsideJersey(viewModel, existingJerseys, top25Ids, "Red");
                SetOutsideJersey(viewModel, existingJerseys, top25Ids, "Green");
                SetOutsideJersey(viewModel, existingJerseys, top25Ids, "Blue");
                SetOutsideJersey(viewModel, existingJerseys, top25Ids, "White");

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
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om scoring gegevens te zien.";
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveScores(ScoringViewModel model)
        {
            try
            {
                if (model.Results == null || model.StageId <= 0)
                    return RedirectToAction("Index");

                var stage = await _context.Stages
                    .FirstOrDefaultAsync(s => s.Id == model.StageId);

                if (stage == null)
                {
                    TempData["Error"] = "Deze rit bestaat niet.";
                    return RedirectToAction("Index");
                }

                var oldResults = await _context.StageResults
                    .Where(r => r.StageId == model.StageId)
                    .ToListAsync();

                var oldJerseys = await _context.Jerseys
                    .Where(j => j.StageId == model.StageId)
                    .ToListAsync();

                _context.StageResults.RemoveRange(oldResults);
                _context.Jerseys.RemoveRange(oldJerseys);

                await _context.SaveChangesAsync();

                var usedJerseys = new HashSet<string>();

                foreach (var row in model.Results.OrderBy(r => r.Position))
                {
                    if (!row.CyclistId.HasValue || row.CyclistId.Value <= 0)
                        continue;

                    _context.StageResults.Add(new StageResult
                    {
                        StageId = model.StageId,
                        CyclistId = row.CyclistId.Value,
                        Position = row.Position,
                        Status = "Finished"
                    });

                    if (row.HasYellowJersey)
                        AddJerseyOnce(model.StageId, row.CyclistId.Value, "Red", usedJerseys);

                    if (row.HasGreenJersey)
                        AddJerseyOnce(model.StageId, row.CyclistId.Value, "Green", usedJerseys);

                    if (row.HasPolkaJersey)
                        AddJerseyOnce(model.StageId, row.CyclistId.Value, "Blue", usedJerseys);

                    if (row.HasWhiteJersey)
                        AddJerseyOnce(model.StageId, row.CyclistId.Value, "White", usedJerseys);
                }

                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.YellowOutsideTop25CyclistId, "Red", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.GreenOutsideTop25CyclistId, "Green", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.PolkaOutsideTop25CyclistId, "Blue", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.WhiteOutsideTop25CyclistId, "White", usedJerseys);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Scores en truidragers succesvol opgeslagen.";

                return RedirectToAction("Index", new
                {
                    raceId = stage.RaceId,
                    stageId = model.StageId
                });
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
                return RedirectToAction("Index", new { stageId = model.StageId });
            }
        }

        private bool HasJersey(List<Jersey> jerseys, int cyclistId, string type)
        {
            return jerseys.Any(j =>
                j.CyclistId == cyclistId &&
                j.Type == type);
        }

        private void SetOutsideJersey(
            ScoringViewModel viewModel,
            List<Jersey> jerseys,
            HashSet<int> top25Ids,
            string type)
        {
            var jersey = jerseys.FirstOrDefault(j =>
                j.Type == type &&
                !top25Ids.Contains(j.CyclistId));

            if (jersey == null)
                return;

            if (type == "Red")
            {
                viewModel.YellowOutsideTop25CyclistId = jersey.CyclistId;
                ViewData["RedOutsideTop25CyclistName"] = jersey.Cyclist?.FullName ?? "";
            }

            if (type == "Green")
            {
                viewModel.GreenOutsideTop25CyclistId = jersey.CyclistId;
                ViewData["GreenOutsideTop25CyclistName"] = jersey.Cyclist?.FullName ?? "";
            }

            if (type == "Blue")
            {
                viewModel.PolkaOutsideTop25CyclistId = jersey.CyclistId;
                ViewData["BlueOutsideTop25CyclistName"] = jersey.Cyclist?.FullName ?? "";
            }

            if (type == "White")
            {
                viewModel.WhiteOutsideTop25CyclistId = jersey.CyclistId;
                ViewData["WhiteOutsideTop25CyclistName"] = jersey.Cyclist?.FullName ?? "";
            }
        }

        private void AddOutsideJerseyIfNotAlreadyUsed(
            int stageId,
            int? cyclistId,
            string type,
            HashSet<string> usedJerseys)
        {
            if (!cyclistId.HasValue || cyclistId.Value <= 0)
                return;

            AddJerseyOnce(stageId, cyclistId.Value, type, usedJerseys);
        }

        private void AddJerseyOnce(
            int stageId,
            int cyclistId,
            string type,
            HashSet<string> usedJerseys)
        {
            if (usedJerseys.Contains(type))
                return;

            _context.Jerseys.Add(new Jersey
            {
                StageId = stageId,
                CyclistId = cyclistId,
                Type = type
            });

            usedJerseys.Add(type);
        }
    }
}