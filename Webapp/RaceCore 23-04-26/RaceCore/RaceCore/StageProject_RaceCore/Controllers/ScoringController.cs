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

        // AANPASSING: stageId = 1 toegevoegd als standaardwaarde. 
        // Dit voorkomt de 404 als je via de navigatie op 'Scoring' klikt zonder parameters.
        public async Task<IActionResult> Index(int stageId = 1)
        {
            // 1. Controleer of de etappe bestaat
            var stage = await _context.Stages.FindAsync(stageId);

            // Als etappe 1 ook niet bestaat (bijv. lege database), sturen we niet naar 404 maar tonen we een melding of pakken de eerste de beste
            if (stage == null)
            {
                stage = await _context.Stages.OrderBy(s => s.Id).FirstOrDefaultAsync();
                if (stage == null) return NotFound("Geen etappes gevonden in de database.");
                stageId = stage.Id;
            }

            // 2. Haal alle renners op voor de autocomplete
            var cyclists = await _context.Cyclists
                .OrderBy(c => c.LastName)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.FirstName + " " + c.LastName
                }).ToListAsync();

            // 3. Haal bestaande data op
            var existingResults = await _context.StageResults
                .Where(r => r.StageId == stageId)
                .Include(r => r.Cyclist)
                .ToListAsync();

            var existingJerseys = await _context.Jerseys
                .Where(j => j.StageId == stageId)
                .ToListAsync();

            // 4. MAAK HET VIEWMODEL AAN
            var viewModel = new ScoringViewModel
            {
                StageId = stageId,
                AvailableCyclists = cyclists,
                Results = new List<StageResultViewModel>()
            };

            // 5. Vul de lijst met 25 rijen
            for (int i = 1; i <= 25; i++)
            {
                var res = existingResults.FirstOrDefault(r => r.Position == i);

                viewModel.Results.Add(new StageResultViewModel
                {
                    Position = i,
                    CyclistId = res?.CyclistId,
                    // Belangrijk: CyclistName vullen zodat de View de naam kan tonen bij herladen
                    CyclistName = res != null ? $"{res.Cyclist.FirstName} {res.Cyclist.LastName}" : "",
                    HasYellowJersey = existingJerseys.Any(j => j.CyclistId == res?.CyclistId && j.Type == "Yellow"),
                    HasGreenJersey = existingJerseys.Any(j => j.CyclistId == res?.CyclistId && j.Type == "Green"),
                    HasPolkaJersey = existingJerseys.Any(j => j.CyclistId == res?.CyclistId && j.Type == "Polka"),
                    HasWhiteJersey = existingJerseys.Any(j => j.CyclistId == res?.CyclistId && j.Type == "White")
                });
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveScores(ScoringViewModel model)
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
                    var result = new StageResult
                    {
                        StageId = model.StageId,
                        CyclistId = row.CyclistId.Value,
                        Position = row.Position,
                        Status = "Finished"
                    };
                    _context.StageResults.Add(result);

                    if (row.HasYellowJersey) AddJersey(model.StageId, row.CyclistId.Value, "Yellow");
                    if (row.HasGreenJersey) AddJersey(model.StageId, row.CyclistId.Value, "Green");
                    if (row.HasPolkaJersey) AddJersey(model.StageId, row.CyclistId.Value, "Polka");
                    if (row.HasWhiteJersey) AddJersey(model.StageId, row.CyclistId.Value, "White");
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", new { stageId = model.StageId });
        }

        private void AddJersey(int sId, int cId, string type)
        {
            _context.Jerseys.Add(new Jersey
            {
                StageId = sId,
                CyclistId = cId,
                Type = type
            });
        }
    }
}