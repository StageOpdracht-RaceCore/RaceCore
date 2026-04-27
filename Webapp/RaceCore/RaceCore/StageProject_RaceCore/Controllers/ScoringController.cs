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

        public async Task<IActionResult> Index(int stageId = 1)
        {
            var viewModel = new ScoringViewModel
            {
                StageId = stageId
            };

            try
            {
                var stage = await _context.Stages.FindAsync(stageId);

                if (stage == null)
                {
                    stage = await _context.Stages.OrderBy(s => s.Id).FirstOrDefaultAsync();

                    if (stage == null)
                    {
                        TempData["Error"] = "Geen etappes gevonden in de database.";
                        return View(viewModel);
                    }

                    stageId = stage.Id;
                    viewModel.StageId = stageId;
                }

                var cyclists = await _context.Cyclists
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.FirstName + " " + c.LastName
                    })
                    .ToListAsync();

                var existingResults = await _context.StageResults
                    .Where(r => r.StageId == stageId)
                    .Include(r => r.Cyclist)
                    .OrderBy(r => r.Position)
                    .ToListAsync();

                var existingJerseys = await _context.Jerseys
                    .Where(j => j.StageId == stageId)
                    .ToListAsync();

                viewModel.AvailableCyclists = cyclists;

                for (int i = 1; i <= 25; i++)
                {
                    var result = existingResults.FirstOrDefault(r => r.Position == i);

                    viewModel.Results.Add(new StageResultViewModel
                    {
                        Position = i,
                        CyclistId = result?.CyclistId,
                        CyclistName = result?.Cyclist != null ? result.Cyclist.FullName : "",
                        HasYellowJersey = result != null && existingJerseys.Any(j => j.CyclistId == result.CyclistId && j.Type == "Yellow"),
                        HasGreenJersey = result != null && existingJerseys.Any(j => j.CyclistId == result.CyclistId && j.Type == "Green"),
                        HasPolkaJersey = result != null && existingJerseys.Any(j => j.CyclistId == result.CyclistId && j.Type == "Polka"),
                        HasWhiteJersey = result != null && existingJerseys.Any(j => j.CyclistId == result.CyclistId && j.Type == "White")
                    });
                }

                var top25Ids = existingResults
                    .Select(r => r.CyclistId)
                    .ToHashSet();

                viewModel.YellowOutsideTop25CyclistId = existingJerseys
                    .FirstOrDefault(j => j.Type == "Yellow" && !top25Ids.Contains(j.CyclistId))
                    ?.CyclistId;

                viewModel.GreenOutsideTop25CyclistId = existingJerseys
                    .FirstOrDefault(j => j.Type == "Green" && !top25Ids.Contains(j.CyclistId))
                    ?.CyclistId;

                viewModel.PolkaOutsideTop25CyclistId = existingJerseys
                    .FirstOrDefault(j => j.Type == "Polka" && !top25Ids.Contains(j.CyclistId))
                    ?.CyclistId;

                viewModel.WhiteOutsideTop25CyclistId = existingJerseys
                    .FirstOrDefault(j => j.Type == "White" && !top25Ids.Contains(j.CyclistId))
                    ?.CyclistId;
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
                if (model.Results == null)
                {
                    return RedirectToAction("Index", new { stageId = model.StageId });
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
                    if (row.CyclistId.HasValue && row.CyclistId.Value > 0)
                    {
                        _context.StageResults.Add(new StageResult
                        {
                            StageId = model.StageId,
                            CyclistId = row.CyclistId.Value,
                            Position = row.Position,
                            Status = "Finished"
                        });

                        if (row.HasYellowJersey)
                            AddJerseyOnce(model.StageId, row.CyclistId.Value, "Yellow", usedJerseys);

                        if (row.HasGreenJersey)
                            AddJerseyOnce(model.StageId, row.CyclistId.Value, "Green", usedJerseys);

                        if (row.HasPolkaJersey)
                            AddJerseyOnce(model.StageId, row.CyclistId.Value, "Polka", usedJerseys);

                        if (row.HasWhiteJersey)
                            AddJerseyOnce(model.StageId, row.CyclistId.Value, "White", usedJerseys);
                    }
                }

                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.YellowOutsideTop25CyclistId, "Yellow", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.GreenOutsideTop25CyclistId, "Green", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.PolkaOutsideTop25CyclistId, "Polka", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.StageId, model.WhiteOutsideTop25CyclistId, "White", usedJerseys);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Scores en truidragers succesvol opgeslagen.";
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
            }

            return RedirectToAction("Index", new { stageId = model.StageId });
        }

        private void AddOutsideJerseyIfNotAlreadyUsed(int stageId, int? cyclistId, string type, HashSet<string> usedJerseys)
        {
            if (!cyclistId.HasValue || cyclistId.Value <= 0)
            {
                return;
            }

            AddJerseyOnce(stageId, cyclistId.Value, type, usedJerseys);
        }

        private void AddJerseyOnce(int stageId, int cyclistId, string type, HashSet<string> usedJerseys)
        {
            if (usedJerseys.Contains(type))
            {
                return;
            }

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