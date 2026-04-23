using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class ScoringController : Controller
    {
        // GET: Scoring/Index
        public IActionResult Index(int stageId)
        {
            // In een echte app haal je deze data uit de DB via je context
            var viewModel = new ScoringViewModel
            {
                StageId = stageId,
                StageName = "Rit " + stageId,
                // We bereiden 25 rijen voor zoals in de Excel
                Results = Enumerable.Range(1, 25).Select(i => new StageResultInput { Position = i }).ToList(),
                AvailableCyclists = GetDummyCyclists() // Vervang door database call
            };

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult SaveScores(ScoringViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Hier komt de "Score Engine" logica:
                // 1. Loop door model.Results
                // 2. Ken punten toe op basis van positie (bijv. 1ste = 100pt, etc.)
                // 3. Voeg trui-bonussen toe
                // 4. Sla op in de database tabel 'StageResults'

                return RedirectToAction("Index", "Leaderboard");
            }
            return View("Index", model);
        }

        private List<SelectListItem> GetDummyCyclists()
        {
            // Simuleert de data van 'TOUR PUNTEN' tabblad
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "1", Text = "Tadej Pogačar" },
                new SelectListItem { Value = "2", Text = "Jonas Vingegaard" },
                new SelectListItem { Value = "3", Text = "Remco Evenepoel" }
            };
        }
    }
}