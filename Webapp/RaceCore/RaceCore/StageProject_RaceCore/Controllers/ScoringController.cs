using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Hubs;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    /* ScoringController.cs
       Purpose: Manage stage scoring input and persistence. Handles
       rendering the scoring UI, validating input (unique cyclist
       checks) and saving StageResults and Jersey data. */
    /// <summary>
    /// Controller for stage scoring (input, validation, save).
    /// </summary>
    public class ScoringController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<GameHub> _hubContext;

        public ScoringController(AppDbContext context, IHubContext<GameHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index(int? raceId, int? stageId, int? gameId)
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

                GameSession? game = null;

                if (gameId.HasValue && gameId.Value > 0)
                {
                    game = await _context.GameSessions
                        .Include(g => g.Race)
                        .FirstOrDefaultAsync(g => g.Id == gameId.Value);
                }

                int selectedRaceId = game?.RaceId
                    ?? raceId
                    ?? (stageId.HasValue
                        ? await _context.Stages
                            .Where(s => s.Id == stageId.Value)
                            .Select(s => s.RaceId)
                            .FirstOrDefaultAsync()
                        : races.First().Id);

                if (selectedRaceId == 0)
                {
                    selectedRaceId = races.First().Id;
                }

                var stages = await _context.Stages
                    .Where(s => s.RaceId == selectedRaceId)
                    .OrderBy(s => s.StageNumber)
                    .ToListAsync();

                if (!stages.Any())
                {
                    TempData["Error"] = "Geen ritten gevonden.";
                    ViewBag.Races = races;
                    ViewBag.SelectedRaceId = selectedRaceId;
                    ViewBag.GameId = game?.Id ?? gameId ?? 0;
                    ViewBag.AvailableStages = new List<SelectListItem>();
                    return View(viewModel);
                }

                int? gameStageId = game?.StageId;

                int selectedStageId =
                    stageId.HasValue && stages.Any(s => s.Id == stageId.Value)
                        ? stageId.Value
                        : gameStageId.HasValue && stages.Any(s => s.Id == gameStageId.Value)
                            ? gameStageId.Value
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

                var results = await _context.StageResults
                    .Where(r => r.StageId == selectedStageId)
                    .Include(r => r.Cyclist)
                    .OrderBy(r => r.Position)
                    .ToListAsync();

                var jerseys = await _context.Jerseys
                    .Where(j => j.StageId == selectedStageId)
                    .Include(j => j.Cyclist)
                    .ToListAsync();

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

                var top25Ids = results
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
                ViewBag.GameId = game?.Id ?? gameId ?? 0;

                ViewBag.AvailableStages = stages.Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"Rit {s.StageNumber} - {s.Name}",
                    Selected = s.Id == selectedStageId
                }).ToList();

                if (game != null)
                {
                    await _hubContext.Clients
                        .Group($"game-{game.Id}")
                        .SendAsync("GoToDashboard", new
                        {
                            gameId = game.Id
                        });
                }
            }
            catch
            {
                TempData["Error"] = "Database fout.";
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveScores(ScoringViewModel model, int raceId, int gameId)
        {
            try
            {
                if (model.StageId <= 0)
                {
                    return RedirectToAction("Index", new { raceId, gameId });
                }

                var stage = await _context.Stages.FindAsync(model.StageId);

                if (stage == null)
                {
                    TempData["Error"] = "Rit niet gevonden.";
                    return RedirectToAction("Index", new { raceId, gameId });
                }

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
                    return RedirectToAction("Index", new { stageId = model.StageId, raceId, gameId });
                }

                var oldResults = _context.StageResults.Where(r => r.StageId == model.StageId);
                var oldJerseys = _context.Jerseys.Where(j => j.StageId == model.StageId);

                _context.StageResults.RemoveRange(oldResults);
                _context.Jerseys.RemoveRange(oldJerseys);

                await _context.SaveChangesAsync();

                var used = new HashSet<string>();

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

                AddOutside(model.StageId, model.YellowOutsideTop25CyclistId, "Red", used);
                AddOutside(model.StageId, model.GreenOutsideTop25CyclistId, "Green", used);
                AddOutside(model.StageId, model.PolkaOutsideTop25CyclistId, "Blue", used);
                AddOutside(model.StageId, model.WhiteOutsideTop25CyclistId, "White", used);

                await _context.SaveChangesAsync();

                if (gameId > 0)
                {
                    await _hubContext.Clients
                        .Group($"game-{gameId}")
                        .SendAsync("ScoresUpdated", new
                        {
                            gameId = gameId,
                            updatedAt = DateTime.Now.Ticks
                        });
                }

                TempData["Success"] = "Opgeslagen!";
                return RedirectToAction("Index", new { stageId = model.StageId, raceId, gameId });
            }
            catch
            {
                TempData["Error"] = "Opslaan mislukt.";
                return RedirectToAction("Index", new { stageId = model.StageId, raceId, gameId });
            }
        }

        private bool HasJersey(List<Jersey> list, int? cyclistId, string type)
        {
            if (!cyclistId.HasValue) return false;
            return list.Any(j => j.CyclistId == cyclistId.Value && j.Type == type);
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

        private string GetOutsideJerseyName(List<Jersey> jerseys, HashSet<int> top25, string type)
        {
            var j = jerseys.FirstOrDefault(x => x.Type == type && !top25.Contains(x.CyclistId));
            return j?.Cyclist?.FullName ?? "";
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
