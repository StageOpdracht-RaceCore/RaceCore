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

        // ============================================================
        // SCORING PAGINA LADEN
        // ============================================================
        public async Task<IActionResult> Index(int? raceId = null, int? gameId = null, int? stageId = null)
        {
            var viewModel = new ScoringViewModel();

            try
            {
                // Alle wedstrijden ophalen voor de dropdown
                var races = await _context.Races
                    .OrderByDescending(r => r.Year)
                    .ThenBy(r => r.Name)
                    .ToListAsync();

                ViewBag.Races = races;

                if (!races.Any())
                {
                    TempData["Error"] = "No races found.";
                    return View(viewModel);
                }

                // Juiste game zoeken
                GameSession? selectedGame = null;

                if (gameId.HasValue)
                {
                    selectedGame = await _context.GameSessions
                        .Include(g => g.Race)
                        .FirstOrDefaultAsync(g => g.Id == gameId.Value);
                }
                else if (raceId.HasValue)
                {
                    selectedGame = await _context.GameSessions
                        .Include(g => g.Race)
                        .Where(g => g.RaceId == raceId.Value)
                        .OrderByDescending(g => g.CreatedAt)
                        .FirstOrDefaultAsync();
                }

                // Fallback naar laatste game
                if (selectedGame == null)
                {
                    selectedGame = await _context.GameSessions
                        .Include(g => g.Race)
                        .OrderByDescending(g => g.CreatedAt)
                        .FirstOrDefaultAsync();
                }

                if (selectedGame == null)
                {
                    TempData["Error"] = "No active game session was found. Start a new game first.";
                    return View(viewModel);
                }

                // Dynamische namen voor truien per wedstrijd
                SetJerseyNames(selectedGame.Race.Name);

                int currentRaceId = selectedGame.RaceId;

                viewModel.GameSessionId = selectedGame.Id;

                // Alle ritten ophalen van deze race
                var allStages = await _context.Stages
                    .Where(s => s.RaceId == currentRaceId)
                    .OrderBy(s => s.StageNumber)
                    .ToListAsync();

                ViewBag.Stages = allStages;

                // Actieve rit bepalen
                Stage? stage = null;

                if (stageId.HasValue)
                {
                    stage = allStages.FirstOrDefault(s => s.Id == stageId.Value);
                }

                if (stage == null)
                {
                    stage = allStages.FirstOrDefault(s => s.StageNumber == selectedGame.CurrentStageNumber)
                            ?? allStages.FirstOrDefault();
                }

                if (stage == null)
                {
                    TempData["Error"] = "No stages found for this race.";
                    return View(viewModel);
                }

                viewModel.StageId = stage.Id;

                ViewBag.SelectedRaceId = currentRaceId;
                ViewBag.SelectedGameId = selectedGame.Id;
                ViewBag.StageNumber = stage.StageNumber;

                // Renners ophalen die gekoppeld zijn aan deze race
                var raceCyclistIds = await _context.RaceEntries
                    .Where(re => re.RaceId == currentRaceId)
                    .Select(re => re.CyclistId)
                    .ToListAsync();

                viewModel.AvailableCyclists = await _context.Cyclists
                    .Where(c => raceCyclistIds.Contains(c.Id))
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.FirstName + " " + c.LastName
                    })
                    .ToListAsync();

                // Bestaande resultaten ophalen voor deze rit
                var results = await _context.StageResults
                    .Where(r => r.GameSessionId == selectedGame.Id && r.StageId == stage.Id)
                    .Include(r => r.Cyclist)
                    .OrderBy(r => r.Position)
                    .ToListAsync();

                // Bestaande truien ophalen voor deze rit
                var jerseys = await _context.Jerseys
                    .Where(j => j.GameSessionId == selectedGame.Id && j.StageId == stage.Id)
                    .Include(j => j.Cyclist)
                    .ToListAsync();

                // Top 25 invullen
                viewModel.Results = new List<StageResultViewModel>();

                for (int i = 1; i <= 25; i++)
                {
                    var result = results.FirstOrDefault(x => x.Position == i);

                    viewModel.Results.Add(new StageResultViewModel
                    {
                        Position = i,
                        CyclistId = result?.CyclistId,
                        CyclistName = result?.Cyclist?.FullName ?? "",

                        HasYellowJersey = result != null && jerseys.Any(j => j.CyclistId == result.CyclistId && j.Type == "Red"),
                        HasGreenJersey = result != null && jerseys.Any(j => j.CyclistId == result.CyclistId && j.Type == "Green"),
                        HasPolkaJersey = result != null && jerseys.Any(j => j.CyclistId == result.CyclistId && j.Type == "Blue"),
                        HasWhiteJersey = result != null && jerseys.Any(j => j.CyclistId == result.CyclistId && j.Type == "White")
                    });
                }

                // Truien buiten Top 25 invullen
                var top25Ids = results.Select(r => r.CyclistId).ToHashSet();
                SetOutsideJerseys(viewModel, jerseys, top25Ids);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error while loading: " + ex.Message;
                return View(viewModel);
            }
        }

        // ============================================================
        // SCORES OPSLAAN
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveScores(ScoringViewModel model, int raceId)
        {
            try
            {
                // Game zoeken
                var game = await _context.GameSessions
                    .Include(g => g.Race)
                    .FirstOrDefaultAsync(g => g.Id == model.GameSessionId);

                if (game == null)
                {
                    TempData["Error"] = "Game sessie niet gevonden.";
                    return RedirectToAction("Index", new { raceId });
                }

                // Rit zoeken
                var stage = await _context.Stages
                    .FirstOrDefaultAsync(s => s.Id == model.StageId);

                if (stage == null)
                {
                    TempData["Error"] = "Rit niet gevonden.";
                    return RedirectToAction("Index", new { raceId, gameId = model.GameSessionId });
                }

                // Enkel ingevulde resultaten gebruiken
                var filledResults = model.Results
                    .Where(r => r.CyclistId.HasValue)
                    .ToList();

                // Dubbele renners in Top 25 blokkeren
                bool hasDuplicateCyclists = filledResults
                    .GroupBy(r => r.CyclistId!.Value)
                    .Any(g => g.Count() > 1);

                if (hasDuplicateCyclists)
                {
                    TempData["Error"] = "A cyclist appears twice in the Top 25.";

                    return RedirectToAction("Index", new
                    {
                        raceId = game.RaceId,
                        gameId = game.Id,
                        stageId = stage.Id
                    });
                }

                // Oude data van deze rit verwijderen zodat opnieuw opslaan proper werkt
                var oldResults = await _context.StageResults
                    .Where(r => r.GameSessionId == game.Id && r.StageId == stage.Id)
                    .ToListAsync();

                var oldJerseys = await _context.Jerseys
                    .Where(j => j.GameSessionId == game.Id && j.StageId == stage.Id)
                    .ToListAsync();

                var oldPlayerPoints = await _context.PlayerPoints
                    .Where(p => p.GameSessionId == game.Id && p.StageId == stage.Id)
                    .ToListAsync();

                _context.StageResults.RemoveRange(oldResults);
                _context.Jerseys.RemoveRange(oldJerseys);
                _context.PlayerPoints.RemoveRange(oldPlayerPoints);

                // Puntenregels ophalen
                var rules = await _context.PointsRules.ToListAsync();

                // Per renner houden we bij hoeveel punten hij krijgt
                var cyclistPoints = new Dictionary<int, int>();

                // Top 25 resultaten opslaan
                foreach (var resultVm in filledResults)
                {
                    int cyclistId = resultVm.CyclistId!.Value;

                    _context.StageResults.Add(new StageResult
                    {
                        GameSessionId = game.Id,
                        StageId = stage.Id,
                        CyclistId = cyclistId,
                        Position = resultVm.Position,
                        Status = "Finished"
                    });

                    // Punten voor positie berekenen
                    int positionPoints = rules
                        .Where(r =>
                            r.Type == "Rit" &&
                            r.FromPosition <= resultVm.Position &&
                            r.ToPosition >= resultVm.Position)
                        .Sum(r => r.Points);

                    AddCyclistPoints(cyclistPoints, cyclistId, positionPoints);

                    // Truien binnen Top 25 opslaan
                    if (resultVm.HasYellowJersey)
                    {
                        AddJerseyAndPoints(game.Id, stage.Id, cyclistId, "Red", cyclistPoints, rules);
                    }

                    if (resultVm.HasGreenJersey)
                    {
                        AddJerseyAndPoints(game.Id, stage.Id, cyclistId, "Green", cyclistPoints, rules);
                    }

                    if (resultVm.HasPolkaJersey)
                    {
                        AddJerseyAndPoints(game.Id, stage.Id, cyclistId, "Blue", cyclistPoints, rules);
                    }

                    if (resultVm.HasWhiteJersey)
                    {
                        AddJerseyAndPoints(game.Id, stage.Id, cyclistId, "White", cyclistPoints, rules);
                    }
                }

                // Truien buiten Top 25 opslaan
                if (model.YellowOutsideTop25CyclistId.HasValue)
                {
                    AddJerseyAndPoints(game.Id, stage.Id, model.YellowOutsideTop25CyclistId.Value, "Red", cyclistPoints, rules);
                }

                if (model.GreenOutsideTop25CyclistId.HasValue)
                {
                    AddJerseyAndPoints(game.Id, stage.Id, model.GreenOutsideTop25CyclistId.Value, "Green", cyclistPoints, rules);
                }

                if (model.PolkaOutsideTop25CyclistId.HasValue)
                {
                    AddJerseyAndPoints(game.Id, stage.Id, model.PolkaOutsideTop25CyclistId.Value, "Blue", cyclistPoints, rules);
                }

                if (model.WhiteOutsideTop25CyclistId.HasValue)
                {
                    AddJerseyAndPoints(game.Id, stage.Id, model.WhiteOutsideTop25CyclistId.Value, "White", cyclistPoints, rules);
                }

                // Punten toekennen aan spelers die deze renners actief hebben geselecteerd
                var playerSelections = await _context.PlayerSelections
                    .Where(ps => ps.GameSessionId == game.Id && ps.IsActive)
                    .ToListAsync();

                foreach (var selection in playerSelections)
                {
                    if (!cyclistPoints.ContainsKey(selection.CyclistId))
                    {
                        continue;
                    }

                    int points = cyclistPoints[selection.CyclistId];

                    if (points <= 0)
                    {
                        continue;
                    }

                    _context.PlayerPoints.Add(new PlayerPoints
                    {
                        GameSessionId = game.Id,
                        PlayerId = selection.PlayerId,
                        RaceId = game.RaceId,
                        StageId = stage.Id,
                        CyclistId = selection.CyclistId,
                        Points = points
                    });
                }

                // Game status updaten
                game.Status = "Active";
                game.CurrentStageNumber = stage.StageNumber;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Scores have been saved to the database.";

                return RedirectToAction("StageResults", "Result", new
                {
                    raceId = game.RaceId,
                    gameId = game.Id
                });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error while saving: " + ex.Message;

                return RedirectToAction("Index", new
                {
                    raceId,
                    gameId = model.GameSessionId,
                    stageId = model.StageId
                });
            }
        }

        // ============================================================
        // JERSEY NAMEN INSTELLEN PER RACE
        // ============================================================
        private void SetJerseyNames(string raceName)
        {
            if (raceName.Contains("Giro", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.YellowJerseyName = "Roze trui";
                ViewBag.GreenJerseyName = "Paarse trui";
                ViewBag.PolkaJerseyName = "Blauwe trui";
                ViewBag.JerseyTheme = "giro-theme";
            }
            else if (raceName.Contains("Tour", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.YellowJerseyName = "Yellow Jersey";
                ViewBag.GreenJerseyName = "Green Jersey";
                ViewBag.PolkaJerseyName = "Bolletjestrui";
                ViewBag.JerseyTheme = "tour-theme";
            }
            else if (raceName.Contains("Vuelta", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.YellowJerseyName = "Red Jersey";
                ViewBag.GreenJerseyName = "Green Jersey";
                ViewBag.PolkaJerseyName = "Bolletjestrui";
                ViewBag.JerseyTheme = "vuelta-theme";
            }
            else
            {
                ViewBag.YellowJerseyName = "Yellow Jersey";
                ViewBag.GreenJerseyName = "Green Jersey";
                ViewBag.PolkaJerseyName = "Bolletjestrui";
                ViewBag.JerseyTheme = "default-theme";
            }
        }

        // ============================================================
        // TRUIEN BUITEN TOP 25 TERUG IN VIEWMODEL LADEN
        // ============================================================
        private void SetOutsideJerseys(ScoringViewModel vm, List<Jersey> jerseys, HashSet<int> top25Ids)
        {
            var red = jerseys.FirstOrDefault(j => j.Type == "Red" && !top25Ids.Contains(j.CyclistId));

            if (red != null)
            {
                vm.YellowOutsideTop25CyclistId = red.CyclistId;
                ViewData["RedOutsideTop25CyclistName"] = red.Cyclist?.FullName;
            }

            var green = jerseys.FirstOrDefault(j => j.Type == "Green" && !top25Ids.Contains(j.CyclistId));

            if (green != null)
            {
                vm.GreenOutsideTop25CyclistId = green.CyclistId;
                ViewData["GreenOutsideTop25CyclistName"] = green.Cyclist?.FullName;
            }

            var blue = jerseys.FirstOrDefault(j => j.Type == "Blue" && !top25Ids.Contains(j.CyclistId));

            if (blue != null)
            {
                vm.PolkaOutsideTop25CyclistId = blue.CyclistId;
                ViewData["BlueOutsideTop25CyclistName"] = blue.Cyclist?.FullName;
            }

            var white = jerseys.FirstOrDefault(j => j.Type == "White" && !top25Ids.Contains(j.CyclistId));

            if (white != null)
            {
                vm.WhiteOutsideTop25CyclistId = white.CyclistId;
                ViewData["WhiteOutsideTop25CyclistName"] = white.Cyclist?.FullName;
            }
        }

        // ============================================================
        // TRUI OPSLAAN EN PUNTEN TOEVOEGEN
        // ============================================================
        private void AddJerseyAndPoints(
            int gameSessionId,
            int stageId,
            int cyclistId,
            string jerseyType,
            Dictionary<int, int> cyclistPoints,
            List<PointsRule> rules)
        {
            _context.Jerseys.Add(new Jersey
            {
                GameSessionId = gameSessionId,
                StageId = stageId,
                CyclistId = cyclistId,
                Type = jerseyType
            });

            int jerseyPoints = rules
                .Where(r => r.Type == ConvertJerseyTypeToRuleType(jerseyType))
                .Sum(r => r.Points);

            AddCyclistPoints(cyclistPoints, cyclistId, jerseyPoints);
        }

        // ============================================================
        // PUNTEN VEILIG OPTELLEN PER RENNER
        // ============================================================
        private void AddCyclistPoints(Dictionary<int, int> cyclistPoints, int cyclistId, int points)
        {
            if (!cyclistPoints.ContainsKey(cyclistId))
            {
                cyclistPoints[cyclistId] = 0;
            }

            cyclistPoints[cyclistId] += points;
        }

        // ============================================================
        // DATABASE TRUI TYPE OMZETTEN NAAR POINTSRULE TYPE
        // ============================================================
        private string ConvertJerseyTypeToRuleType(string type)
        {
            return type switch
            {
                "Red" => "RodeTrui",
                "Green" => "GroeneTrui",
                "Blue" => "BlauweTrui",
                "White" => "WitteTrui",
                _ => type
            };
        }
    }
}