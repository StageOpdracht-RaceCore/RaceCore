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

        public async Task<IActionResult> Index(int? raceId = null, int? gameId = null, int? stageId = null)
        {
            var viewModel = new ScoringViewModel();

            try
            {
                // 1. Haal alle wedstrijden op voor de dropdown
                var races = await _context.Races
                    .OrderByDescending(r => r.Year)
                    .ThenBy(r => r.Name)
                    .ToListAsync();
                ViewBag.Races = races;

                if (!races.Any())
                {
                    TempData["Error"] = "Geen wedstrijden gevonden.";
                    return View(viewModel);
                }

                // 2. Bepaal de juiste GameSession op basis van de gekozen raceId of gameId
                GameSession? selectedGame = null;

                if (gameId.HasValue)
                {
                    selectedGame = await _context.GameSessions
                        .Include(g => g.Race)
                        .FirstOrDefaultAsync(g => g.Id == gameId.Value);
                }
                else if (raceId.HasValue)
                {
                    // CRUCIAAL: Als er een raceId is gekozen, zoek de meest recente game voor die race
                    selectedGame = await _context.GameSessions
                        .Include(g => g.Race)
                        .Where(g => g.RaceId == raceId.Value)
                        .OrderByDescending(g => g.CreatedAt)
                        .FirstOrDefaultAsync();
                }

                // Fallback: Als er nog steeds niets is, pak de allerlaatste game ooit gemaakt
                if (selectedGame == null)
                {
                    selectedGame = await _context.GameSessions
                        .Include(g => g.Race)
                        .OrderByDescending(g => g.CreatedAt)
                        .FirstOrDefaultAsync();
                }

                if (selectedGame == null)
                {
                    TempData["Error"] = "Er is geen actieve game sessie gevonden. Start eerst een nieuwe game.";
                    return View(viewModel);
                }

                // --- DYNAMISCHE THEMA LOGICA ---
                var raceName = selectedGame.Race.Name;
                SetJerseyNames(raceName);

                int currentRaceId = selectedGame.RaceId;
                viewModel.GameSessionId = selectedGame.Id;

                // 3. Haal alle ritten op voor de gekozen wedstrijd
                var allStages = await _context.Stages
                    .Where(s => s.RaceId == currentRaceId)
                    .OrderBy(s => s.StageNumber)
                    .ToListAsync();
                ViewBag.Stages = allStages;

                // 4. Bepaal de actieve etappe
                Stage? stage = null;
                if (stageId.HasValue)
                {
                    stage = allStages.FirstOrDefault(s => s.Id == stageId.Value);
                }

                // Fallback naar de huidige etappe van de game sessie
                if (stage == null)
                {
                    stage = allStages.FirstOrDefault(s => s.StageNumber == selectedGame.CurrentStageNumber)
                            ?? allStages.FirstOrDefault();
                }

                if (stage == null)
                {
                    TempData["Error"] = "Geen ritten gevonden voor deze wedstrijd.";
                    return View(viewModel);
                }

                // Zet data voor de View
                viewModel.StageId = stage.Id;
                ViewBag.SelectedRaceId = currentRaceId;
                ViewBag.SelectedGameId = selectedGame.Id;
                ViewBag.StageNumber = stage.StageNumber;

                // 5. Haal renners en resultaten op
                var raceCyclistIds = await _context.RaceEntries
                    .Where(re => re.RaceId == currentRaceId)
                    .Select(re => re.CyclistId)
                    .ToListAsync();

                viewModel.AvailableCyclists = await _context.Cyclists
                    .Where(c => raceCyclistIds.Contains(c.Id))
                    .OrderBy(c => c.LastName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.FirstName + " " + c.LastName
                    }).ToListAsync();

                var results = await _context.StageResults
                    .Where(r => r.GameSessionId == selectedGame.Id && r.StageId == stage.Id)
                    .Include(r => r.Cyclist)
                    .OrderBy(r => r.Position)
                    .ToListAsync();

                var jerseys = await _context.Jerseys
                    .Where(j => j.GameSessionId == selectedGame.Id && j.StageId == stage.Id)
                    .Include(j => j.Cyclist)
                    .ToListAsync();

                // Vul de Top 25
                viewModel.Results = new List<StageResultViewModel>();
                for (int i = 1; i <= 25; i++)
                {
                    var result = results.FirstOrDefault(x => x.Position == i);
                    viewModel.Results.Add(new StageResultViewModel
                    {
                        Position = i,
                        CyclistId = result?.CyclistId,
                        CyclistName = result?.Cyclist?.FullName ?? "",
                        HasYellowJersey = jerseys.Any(j => j.CyclistId == result?.CyclistId && j.Type == "Red"),
                        HasGreenJersey = jerseys.Any(j => j.CyclistId == result?.CyclistId && j.Type == "Green"),
                        HasPolkaJersey = jerseys.Any(j => j.CyclistId == result?.CyclistId && j.Type == "Blue"),
                        HasWhiteJersey = jerseys.Any(j => j.CyclistId == result?.CyclistId && j.Type == "White")
                    });
                }

                var top25Ids = results.Select(r => r.CyclistId).ToHashSet();
                SetOutsideJerseys(viewModel, jerseys, top25Ids);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Fout bij laden: " + ex.Message;
                return View(viewModel);
            }
        }

        private void SetJerseyNames(string raceName)
        {
            if (raceName.Contains("Giro", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.YellowJerseyName = "Roze trui"; ViewBag.GreenJerseyName = "Paarse trui";
                ViewBag.PolkaJerseyName = "Blauwe trui"; ViewBag.JerseyTheme = "giro-theme";
            }
            else if (raceName.Contains("Tour", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.YellowJerseyName = "Gele trui"; ViewBag.GreenJerseyName = "Groene trui";
                ViewBag.PolkaJerseyName = "Bolletjestrui"; ViewBag.JerseyTheme = "tour-theme";
            }
            else if (raceName.Contains("Vuelta", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.YellowJerseyName = "Rode trui"; ViewBag.GreenJerseyName = "Groene trui";
                ViewBag.PolkaJerseyName = "Bolletjestrui"; ViewBag.JerseyTheme = "vuelta-theme";
            }
            else
            {
                ViewBag.YellowJerseyName = "Gele trui"; ViewBag.GreenJerseyName = "Groene trui";
                ViewBag.PolkaJerseyName = "Bolletjestrui"; ViewBag.JerseyTheme = "default-theme";
            }
        }

        private void SetOutsideJerseys(ScoringViewModel vm, List<Jersey> jerseys, HashSet<int> top25Ids)
        {
            var red = jerseys.FirstOrDefault(j => j.Type == "Red" && !top25Ids.Contains(j.CyclistId));
            if (red != null) { vm.YellowOutsideTop25CyclistId = red.CyclistId; ViewData["RedOutsideTop25CyclistName"] = red.Cyclist?.FullName; }
            var green = jerseys.FirstOrDefault(j => j.Type == "Green" && !top25Ids.Contains(j.CyclistId));
            if (green != null) { vm.GreenOutsideTop25CyclistId = green.CyclistId; ViewData["GreenOutsideTop25CyclistName"] = green.Cyclist?.FullName; }
            var blue = jerseys.FirstOrDefault(j => j.Type == "Blue" && !top25Ids.Contains(j.CyclistId));
            if (blue != null) { vm.PolkaOutsideTop25CyclistId = blue.CyclistId; ViewData["BlueOutsideTop25CyclistName"] = blue.Cyclist?.FullName; }
            var white = jerseys.FirstOrDefault(j => j.Type == "White" && !top25Ids.Contains(j.CyclistId));
            if (white != null) { vm.WhiteOutsideTop25CyclistId = white.CyclistId; ViewData["WhiteOutsideTop25CyclistName"] = white.Cyclist?.FullName; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveScores(ScoringViewModel model, int raceId)
        {
            // Opslaglogica...
            return RedirectToAction("StageResults", "Result", new { raceId = raceId });
        }
    }
}