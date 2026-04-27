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
                StageId = stageId,
                AvailableCyclists = new List<SelectListItem>(),
                Results = new List<StageResultViewModel>()
            };

            for (int i = 1; i <= 25; i++)
            {
                viewModel.Results.Add(new StageResultViewModel { Position = i });
            }

            try
            {
                var stage = await _context.Stages.FindAsync(stageId);

                if (stage == null)
                {
                    stage = await _context.Stages.OrderBy(s => s.Id).FirstOrDefaultAsync();
                    if (stage == null)
                    {
                        ViewBag.DatabaseOnline = true;
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
                    .ToListAsync();

                var existingJerseys = await _context.Jerseys
                    .Where(j => j.StageId == stageId)
                    .ToListAsync();

                viewModel.AvailableCyclists = cyclists;
                viewModel.Results = new List<StageResultViewModel>();

                for (int i = 1; i <= 25; i++)
                {
                    var res = existingResults.FirstOrDefault(r => r.Position == i);

                    viewModel.Results.Add(new StageResultViewModel
                    {
                        Position = i,
                        CyclistId = res?.CyclistId,
                        CyclistName = res != null && res.Cyclist != null ? $"{res.Cyclist.FirstName} {res.Cyclist.LastName}" : "",
                        HasYellowJersey = existingJerseys.Any(j => j.CyclistId == res?.CyclistId && j.Type == "Yellow"),
                        HasGreenJersey = existingJerseys.Any(j => j.CyclistId == res?.CyclistId && j.Type == "Green"),
                        HasPolkaJersey = existingJerseys.Any(j => j.CyclistId == res?.CyclistId && j.Type == "Polka"),
                        HasWhiteJersey = existingJerseys.Any(j => j.CyclistId == res?.CyclistId && j.Type == "White")
                    });
                }

                ViewBag.DatabaseOnline = true;
            }
            catch
            {
                ViewBag.DatabaseOnline = false;
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
                if (model.Results == null) return RedirectToAction("Index", new { stageId = model.StageId });

                var oldResults = _context.StageResults.Where(r => r.StageId == model.StageId);
                var oldJerseys = _context.Jerseys.Where(j => j.StageId == model.StageId);

                _context.StageResults.RemoveRange(oldResults);
                _context.Jerseys.RemoveRange(oldJerseys);

                foreach (var row in model.Results)
                {
                    if (row.CyclistId.HasValue && row.CyclistId > 0)
                    {
                        _context.StageResults.Add(new StageResult
                        {
                            StageId = model.StageId,
                            CyclistId = row.CyclistId.Value,
                            Position = row.Position,
                            Status = "Finished"
                        });

                        if (row.HasYellowJersey) AddJersey(model.StageId, row.CyclistId.Value, "Yellow");
                        if (row.HasGreenJersey) AddJersey(model.StageId, row.CyclistId.Value, "Green");
                        if (row.HasPolkaJersey) AddJersey(model.StageId, row.CyclistId.Value, "Polka");
                        if (row.HasWhiteJersey) AddJersey(model.StageId, row.CyclistId.Value, "White");
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
            }

            return RedirectToAction("Index", new { stageId = model.StageId });
        }

        private void AddJersey(int stageId, int cyclistId, string type)
        {
            _context.Jerseys.Add(new Jersey
            {
                StageId = stageId,
                CyclistId = cyclistId,
                Type = type
            });
        }
    }
}
