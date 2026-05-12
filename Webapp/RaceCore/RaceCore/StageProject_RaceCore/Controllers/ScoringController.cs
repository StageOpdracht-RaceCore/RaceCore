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

        public async Task<IActionResult> Index(int? raceId = null, int? stageId = null, int? gameId = null)
        {
            var viewModel = new ScoringViewModel();

            try
            {
                var races = await _context.Races
                    .OrderByDescending(r => r.Year)
                    .ThenBy(r => r.Name)
                    .ToListAsync();

                if (!races.Any())
                {
                    TempData["Error"] = "Geen wedstrijden gevonden.";
                    return View(viewModel);
                }

                int selectedRaceId = raceId ?? races.First().Id;

                var selectedGame = gameId.HasValue
                    ? await _context.GameSessions
                        .Include(g => g.Race)
                        .FirstOrDefaultAsync(g => g.Id == gameId.Value)
                    : await _context.GameSessions
                        .Include(g => g.Race)
                        .Where(g => g.RaceId == selectedRaceId)
                        .OrderByDescending(g => g.CreatedAt)
                        .FirstOrDefaultAsync();

                if (selectedGame == null)
                {
                    TempData["Error"] = "Geen game gevonden voor deze wedstrijd.";
                    ViewBag.Races = races;
                    ViewBag.SelectedRaceId = selectedRaceId;
                    ViewBag.AvailableStages = new List<SelectListItem>();
                    return View(viewModel);
                }

                selectedRaceId = selectedGame.RaceId;
                viewModel.GameSessionId = selectedGame.Id;

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

                int selectedStageId = stageId.HasValue && stages.Any(s => s.Id == stageId.Value)
                    ? stageId.Value
                    : selectedGame.StageId > 0 && stages.Any(s => s.Id == selectedGame.StageId)
                        ? selectedGame.StageId
                        : stages.First().Id;

                viewModel.StageId = selectedStageId;

                var raceCyclistIds = await _context.RaceEntries
                    .Where(re => re.RaceId == selectedRaceId)
                    .Select(re => re.CyclistId)
                    .ToListAsync();

                var cyclistsQuery = _context.Cyclists
                    .Include(c => c.Team)
                    .AsQueryable();

                if (raceCyclistIds.Any())
                {
                    cyclistsQuery = cyclistsQuery.Where(c => raceCyclistIds.Contains(c.Id));
                }

                viewModel.AvailableCyclists = await cyclistsQuery
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.FirstName + " " + c.LastName
                    })
                    .ToListAsync();

                var results = await _context.StageResults
                    .Where(r => r.GameSessionId == selectedGame.Id && r.StageId == selectedStageId)
                    .Include(r => r.Cyclist)
                    .OrderBy(r => r.Position)
                    .ToListAsync();

                var jerseys = await _context.Jerseys
                    .Where(j => j.GameSessionId == selectedGame.Id && j.StageId == selectedStageId)
                    .Include(j => j.Cyclist)
                    .ToListAsync();

                for (int i = 1; i <= 25; i++)
                {
                    var result = results.FirstOrDefault(x => x.Position == i);

                    viewModel.Results.Add(new StageResultViewModel
                    {
                        Position = i,
                        CyclistId = result?.CyclistId,
                        CyclistName = result?.Cyclist?.FullName ?? "",
                        HasYellowJersey = HasJersey(jerseys, result?.CyclistId, "Red"),
                        HasGreenJersey = HasJersey(jerseys, result?.CyclistId, "Green"),
                        HasPolkaJersey = HasJersey(jerseys, result?.CyclistId, "Blue"),
                        HasWhiteJersey = HasJersey(jerseys, result?.CyclistId, "White")
                    });
                }

                var top25Ids = results
                    .Where(r => r.CyclistId > 0)
                    .Select(r => r.CyclistId)
                    .ToHashSet();

                SetOutsideJersey(viewModel, jerseys, top25Ids, "Red");
                SetOutsideJersey(viewModel, jerseys, top25Ids, "Green");
                SetOutsideJersey(viewModel, jerseys, top25Ids, "Blue");
                SetOutsideJersey(viewModel, jerseys, top25Ids, "White");

                ViewData["RedOutsideTop25CyclistName"] = GetOutsideJerseyName(jerseys, top25Ids, "Red");
                ViewData["GreenOutsideTop25CyclistName"] = GetOutsideJerseyName(jerseys, top25Ids, "Green");
                ViewData["BlueOutsideTop25CyclistName"] = GetOutsideJerseyName(jerseys, top25Ids, "Blue");
                ViewData["WhiteOutsideTop25CyclistName"] = GetOutsideJerseyName(jerseys, top25Ids, "White");

                ViewBag.Races = races;
                ViewBag.SelectedRaceId = selectedRaceId;
                ViewBag.SelectedGameId = selectedGame.Id;
                ViewBag.AvailableStages = stages.Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"Rit {s.StageNumber} - {s.Name}",
                    Selected = s.Id == selectedStageId
                }).ToList();

                return View(viewModel);
            }
            catch
            {
                TempData["Error"] = "Database fout.";
                return View(viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveScores(ScoringViewModel model, int raceId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (model.GameSessionId <= 0 || model.StageId <= 0)
                {
                    TempData["Error"] = "Game of rit ontbreekt.";
                    return RedirectToAction("Index", new { raceId, stageId = model.StageId });
                }

                var game = await _context.GameSessions
                    .Include(g => g.Race)
                    .FirstOrDefaultAsync(g => g.Id == model.GameSessionId);

                if (game == null)
                {
                    TempData["Error"] = "Game niet gevonden.";
                    return RedirectToAction("Index", new { raceId, stageId = model.StageId });
                }

                var stage = await _context.Stages
                    .FirstOrDefaultAsync(s => s.Id == model.StageId && s.RaceId == game.RaceId);

                if (stage == null)
                {
                    TempData["Error"] = "Rit hoort niet bij deze game.";
                    return RedirectToAction("Index", new { raceId = game.RaceId });
                }

                var ids = model.Results
                    .Where(r => r.CyclistId.HasValue)
                    .Select(r => r.CyclistId!.Value)
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

                    return RedirectToAction("Index", new
                    {
                        raceId = game.RaceId,
                        stageId = model.StageId,
                        gameId = model.GameSessionId
                    });
                }

                var oldResults = await _context.StageResults
                    .Where(r => r.GameSessionId == model.GameSessionId && r.StageId == model.StageId)
                    .ToListAsync();

                var oldJerseys = await _context.Jerseys
                    .Where(j => j.GameSessionId == model.GameSessionId && j.StageId == model.StageId)
                    .ToListAsync();

                var oldPlayerPoints = await _context.PlayerPoints
                    .Where(pp => pp.GameSessionId == model.GameSessionId && pp.StageId == model.StageId)
                    .ToListAsync();

                _context.StageResults.RemoveRange(oldResults);
                _context.Jerseys.RemoveRange(oldJerseys);
                _context.PlayerPoints.RemoveRange(oldPlayerPoints);

                await _context.SaveChangesAsync();

                var usedJerseys = new HashSet<string>();

                foreach (var result in model.Results.OrderBy(x => x.Position))
                {
                    if (!result.CyclistId.HasValue)
                    {
                        continue;
                    }

                    _context.StageResults.Add(new StageResult
                    {
                        GameSessionId = model.GameSessionId,
                        StageId = model.StageId,
                        CyclistId = result.CyclistId.Value,
                        Position = result.Position,
                        Status = "Finished"
                    });

                    if (result.HasYellowJersey)
                    {
                        AddJerseyOnce(model.GameSessionId, model.StageId, result.CyclistId.Value, "Red", usedJerseys);
                    }

                    if (result.HasGreenJersey)
                    {
                        AddJerseyOnce(model.GameSessionId, model.StageId, result.CyclistId.Value, "Green", usedJerseys);
                    }

                    if (result.HasPolkaJersey)
                    {
                        AddJerseyOnce(model.GameSessionId, model.StageId, result.CyclistId.Value, "Blue", usedJerseys);
                    }

                    if (result.HasWhiteJersey)
                    {
                        AddJerseyOnce(model.GameSessionId, model.StageId, result.CyclistId.Value, "White", usedJerseys);
                    }
                }

                AddOutsideJerseyIfNotAlreadyUsed(model.GameSessionId, model.StageId, model.YellowOutsideTop25CyclistId, "Red", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.GameSessionId, model.StageId, model.GreenOutsideTop25CyclistId, "Green", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.GameSessionId, model.StageId, model.PolkaOutsideTop25CyclistId, "Blue", usedJerseys);
                AddOutsideJerseyIfNotAlreadyUsed(model.GameSessionId, model.StageId, model.WhiteOutsideTop25CyclistId, "White", usedJerseys);

                game.CurrentStageNumber = stage.StageNumber;

                if (game.Status == "Draft")
                {
                    game.Status = "Active";
                }

                await _context.SaveChangesAsync();

                await RebuildPlayerPointsForStage(model.GameSessionId, model.StageId);

                await transaction.CommitAsync();

                TempData["Success"] = "Scores gepubliceerd.";

                return RedirectToAction("StageResults", "Result", new
                {
                    raceId = game.RaceId
                });
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["Error"] = "Opslaan mislukt.";

                return RedirectToAction("Index", new
                {
                    raceId,
                    stageId = model.StageId,
                    gameId = model.GameSessionId
                });
            }
        }

        private async Task RebuildPlayerPointsForStage(int gameSessionId, int stageId)
        {
            var game = await _context.GameSessions
                .FirstAsync(g => g.Id == gameSessionId);

            var oldPoints = await _context.PlayerPoints
                .Where(pp => pp.GameSessionId == gameSessionId && pp.StageId == stageId)
                .ToListAsync();

            _context.PlayerPoints.RemoveRange(oldPoints);
            await _context.SaveChangesAsync();

            var rules = await _context.PointsRules.ToListAsync();

            var results = await _context.StageResults
                .Where(sr => sr.GameSessionId == gameSessionId && sr.StageId == stageId)
                .ToListAsync();

            var jerseys = await _context.Jerseys
                .Where(j => j.GameSessionId == gameSessionId && j.StageId == stageId)
                .ToListAsync();

            var pointsByCyclist = new Dictionary<int, int>();

            foreach (var result in results)
            {
                if (!result.Position.HasValue)
                {
                    continue;
                }

                int points = rules
                    .Where(r =>
                        r.Type == "Rit" &&
                        r.FromPosition <= result.Position.Value &&
                        r.ToPosition >= result.Position.Value)
                    .Sum(r => r.Points);

                if (!pointsByCyclist.ContainsKey(result.CyclistId))
                {
                    pointsByCyclist[result.CyclistId] = 0;
                }

                pointsByCyclist[result.CyclistId] += points;
            }

            foreach (var jersey in jerseys)
            {
                int points = rules
                    .Where(r => r.Type == GetRuleTypeForJersey(jersey.Type))
                    .Sum(r => r.Points);

                if (!pointsByCyclist.ContainsKey(jersey.CyclistId))
                {
                    pointsByCyclist[jersey.CyclistId] = 0;
                }

                pointsByCyclist[jersey.CyclistId] += points;
            }

            var selections = await _context.PlayerSelections
                .Where(ps => ps.GameSessionId == gameSessionId)
                .ToListAsync();

            foreach (var selection in selections)
            {
                if (!pointsByCyclist.ContainsKey(selection.CyclistId))
                {
                    continue;
                }

                int points = pointsByCyclist[selection.CyclistId];

                if (points <= 0)
                {
                    continue;
                }

                _context.PlayerPoints.Add(new PlayerPoints
                {
                    GameSessionId = gameSessionId,
                    PlayerId = selection.PlayerId,
                    RaceId = game.RaceId,
                    StageId = stageId,
                    CyclistId = selection.CyclistId,
                    Points = points
                });
            }

            await _context.SaveChangesAsync();
        }

        private static bool HasJersey(List<Jersey> jerseys, int? cyclistId, string type)
        {
            if (!cyclistId.HasValue)
            {
                return false;
            }

            return jerseys.Any(j => j.CyclistId == cyclistId.Value && j.Type == type);
        }

        private static void SetOutsideJersey(
            ScoringViewModel viewModel,
            List<Jersey> jerseys,
            HashSet<int> top25,
            string type)
        {
            var jersey = jerseys.FirstOrDefault(j => j.Type == type && !top25.Contains(j.CyclistId));

            if (jersey == null)
            {
                return;
            }

            if (type == "Red") viewModel.YellowOutsideTop25CyclistId = jersey.CyclistId;
            if (type == "Green") viewModel.GreenOutsideTop25CyclistId = jersey.CyclistId;
            if (type == "Blue") viewModel.PolkaOutsideTop25CyclistId = jersey.CyclistId;
            if (type == "White") viewModel.WhiteOutsideTop25CyclistId = jersey.CyclistId;
        }

        private static string GetOutsideJerseyName(List<Jersey> jerseys, HashSet<int> top25, string type)
        {
            var jersey = jerseys.FirstOrDefault(j => j.Type == type && !top25.Contains(j.CyclistId));

            return jersey?.Cyclist?.FullName ?? "";
        }

        private void AddOutsideJerseyIfNotAlreadyUsed(
            int gameSessionId,
            int stageId,
            int? cyclistId,
            string type,
            HashSet<string> used)
        {
            if (!cyclistId.HasValue)
            {
                return;
            }

            AddJerseyOnce(gameSessionId, stageId, cyclistId.Value, type, used);
        }

        private void AddJerseyOnce(
            int gameSessionId,
            int stageId,
            int cyclistId,
            string type,
            HashSet<string> used)
        {
            if (used.Contains(type))
            {
                return;
            }

            _context.Jerseys.Add(new Jersey
            {
                GameSessionId = gameSessionId,
                StageId = stageId,
                CyclistId = cyclistId,
                Type = type
            });

            used.Add(type);
        }

        private static string GetRuleTypeForJersey(string jerseyType)
        {
            return jerseyType switch
            {
                "Red" => "RodeTrui",
                "Green" => "GroeneTrui",
                "Blue" => "BlauweTrui",
                "White" => "WitteTrui",
                "Yellow" => "RodeTrui",
                "Polka" => "BlauweTrui",
                "RodeTrui" => "RodeTrui",
                "GroeneTrui" => "GroeneTrui",
                "BlauweTrui" => "BlauweTrui",
                "WitteTrui" => "WitteTrui",
                _ => jerseyType
            };
        }
    }
}