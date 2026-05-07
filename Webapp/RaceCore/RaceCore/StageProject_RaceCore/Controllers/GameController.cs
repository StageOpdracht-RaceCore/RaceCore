using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    /* GameController.cs
       Purpose: Handle creation and lifecycle of GameSession objects.
       Responsibilities include creating new games, keeping track of
       host sessions, closing stale games and providing helper APIs
       used by the UI (stages list by race). */
    /// <summary>
    /// Controller to create and manage game sessions (New, Host ping, helpers).
    /// </summary>
    public class GameController : Controller
    {
        private readonly AppDbContext _context;

        private const int HostTimeoutSeconds = 60;
        private const int ActiveRidersPerPlayer = 10;
        private const int BenchRidersPerPlayer = 5;

        public GameController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> New()
        {
            await CloseDeadHostGames();
            await SetActiveGameViewBag();

            var model = await BuildNewGameViewModelSafe();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetStagesByRace(int raceId)
        {
            var stages = await _context.Stages
                .Where(s => s.RaceId == raceId)
                .OrderBy(s => s.StageNumber)
                .Select(s => new
                {
                    id = s.Id,
                    text = "Rit " + s.StageNumber + " - " + s.Name
                })
                .ToListAsync();

            return Json(stages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(NewGameViewModel model)
        {
            await CloseDeadHostGames();

            model.SelectedPlayerIds = model.SelectedPlayerIds
                .Distinct()
                .ToList();

            if (model.RaceId <= 0)
            {
                ModelState.AddModelError(nameof(model.RaceId), "Kies een race.");
            }

            if (model.StageId <= 0)
            {
                ModelState.AddModelError(nameof(model.StageId), "Kies een rit.");
            }

            if (model.SelectedPlayerIds.Count < 2)
            {
                ModelState.AddModelError(nameof(model.SelectedPlayerIds), "Kies minstens 2 spelers.");
            }

            if (!ModelState.IsValid)
            {
                await SetActiveGameViewBag();
                return View(await BuildNewGameViewModelSafe(model.RaceId, model.StageId, model.SelectedPlayerIds));
            }

            try
            {
                var race = await _context.Races
                    .FirstOrDefaultAsync(r => r.Id == model.RaceId);

                if (race == null)
                {
                    TempData["Error"] = "Race niet gevonden.";
                    await SetActiveGameViewBag();
                    return View(await BuildNewGameViewModelSafe(model.RaceId, model.StageId, model.SelectedPlayerIds));
                }

                var stage = await _context.Stages
                    .FirstOrDefaultAsync(s => s.Id == model.StageId && s.RaceId == model.RaceId);

                if (stage == null)
                {
                    TempData["Error"] = "Rit niet gevonden bij deze race.";
                    await SetActiveGameViewBag();
                    return View(await BuildNewGameViewModelSafe(model.RaceId, model.StageId, model.SelectedPlayerIds));
                }

                var players = await _context.Players
                    .Where(p => model.SelectedPlayerIds.Contains(p.Id))
                    .OrderBy(p => p.PositionInDraft)
                    .ThenBy(p => p.Id)
                    .ToListAsync();

                if (players.Count < 2)
                {
                    TempData["Error"] = "Kies minstens 2 geldige spelers.";
                    await SetActiveGameViewBag();
                    return View(await BuildNewGameViewModelSafe(model.RaceId, model.StageId, model.SelectedPlayerIds));
                }

                string hostSessionId = GetOrCreateHostSessionId();

                var game = new GameSession
                {
                    RaceId = model.RaceId,
                    StageId = model.StageId,
                    Status = "Draft",
                    CurrentStageNumber = stage.StageNumber,
                    RidersPerPlayer = ActiveRidersPerPlayer,
                    BenchPerPlayer = BenchRidersPerPlayer,
                    CreatedAt = DateTime.Now,
                    HostSessionId = hostSessionId,
                    LastHostPingAt = DateTime.Now
                };

                _context.GameSessions.Add(game);
                await _context.SaveChangesAsync();

                int totalRounds = game.RidersPerPlayer + game.BenchPerPlayer;

                var draftTurns = GenerateFairSnakeDraft(game.Id, race.Id, players, totalRounds);

                _context.DraftTurns.AddRange(draftTurns);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Game {race.Name} {race.Year} - Rit {stage.StageNumber} is gestart.";

                return RedirectToAction("Index", "Draft", new { gameId = game.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Start Game fout: " + ex.Message;
                await SetActiveGameViewBag();
                return View(await BuildNewGameViewModelSafe(model.RaceId, model.StageId, model.SelectedPlayerIds));
            }
        }

        [HttpPost]
        public async Task<IActionResult> HostPing(int gameId)
        {
            string? hostSessionId = HttpContext.Session.GetString("RaceCoreHostSessionId");

            if (string.IsNullOrWhiteSpace(hostSessionId))
            {
                return Json(new { success = false, reason = "No host session" });
            }

            var game = await _context.GameSessions
                .FirstOrDefaultAsync(g =>
                    g.Id == gameId &&
                    g.HostSessionId == hostSessionId &&
                    (g.Status == "Draft" || g.Status == "Active"));

            if (game == null)
            {
                return Json(new { success = false, reason = "Game not found or not host" });
            }

            game.LastHostPingAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        private async Task SetActiveGameViewBag()
        {
            DateTime limit = DateTime.Now.AddSeconds(-HostTimeoutSeconds);

            var activeGame = await _context.GameSessions
                .Include(g => g.Race)
                .Include(g => g.Stage)
                .Where(g =>
                    (g.Status == "Draft" || g.Status == "Active") &&
                    g.LastHostPingAt != null &&
                    g.LastHostPingAt >= limit)
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync();

            ViewBag.ActiveGameId = activeGame?.Id ?? 0;

            ViewBag.ActiveGameRaceName = activeGame?.Race != null
                ? activeGame.Race.Name + " " + activeGame.Race.Year
                : "";

            ViewBag.ActiveGameStageName = activeGame?.Stage != null
                ? "Rit " + activeGame.Stage.StageNumber + " - " + activeGame.Stage.Name
                : "";

            ViewBag.ActiveGameStatus = activeGame?.Status ?? "";
        }

        private async Task CloseDeadHostGames()
        {
            DateTime limit = DateTime.Now.AddSeconds(-HostTimeoutSeconds);

            var oldGames = await _context.GameSessions
                .Where(g =>
                    (g.Status == "Draft" || g.Status == "Active") &&
                    (g.LastHostPingAt == null || g.LastHostPingAt < limit))
                .ToListAsync();

            if (!oldGames.Any())
            {
                return;
            }

            foreach (var game in oldGames)
            {
                game.Status = "Cancelled";
            }

            await _context.SaveChangesAsync();
        }

        private string GetOrCreateHostSessionId()
        {
            string? hostSessionId = HttpContext.Session.GetString("RaceCoreHostSessionId");

            if (string.IsNullOrWhiteSpace(hostSessionId))
            {
                hostSessionId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("RaceCoreHostSessionId", hostSessionId);
            }

            return hostSessionId;
        }

        private async Task<NewGameViewModel> BuildNewGameViewModelSafe(
            int selectedRaceId = 0,
            int selectedStageId = 0,
            List<int>? selectedPlayerIds = null)
        {
            try
            {
                return await BuildNewGameViewModel(selectedRaceId, selectedStageId, selectedPlayerIds);
            }
            catch
            {
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om races en spelers te laden.";

                return new NewGameViewModel
                {
                    RaceId = selectedRaceId,
                    StageId = selectedStageId,
                    SelectedPlayerIds = selectedPlayerIds ?? new List<int>(),
                    AvailableRaces = new List<SelectListItem>(),
                    AvailableStages = new List<SelectListItem>(),
                    AvailablePlayers = new List<PlayerSelectItemViewModel>(),
                    TotalStages = 0,
                    TotalCyclists = 0
                };
            }
        }

        private async Task<NewGameViewModel> BuildNewGameViewModel(
            int selectedRaceId = 0,
            int selectedStageId = 0,
            List<int>? selectedPlayerIds = null)
        {
            selectedPlayerIds ??= new List<int>();

            var races = await _context.Races
                .Include(r => r.Stages)
                .OrderByDescending(r => r.Year)
                .ThenBy(r => r.Name)
                .ToListAsync();

            var players = await _context.Players
                .OrderBy(p => p.PositionInDraft)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var selectedRace = selectedRaceId > 0
                ? races.FirstOrDefault(r => r.Id == selectedRaceId)
                : races.FirstOrDefault();

            int raceId = selectedRace?.Id ?? 0;

            var stages = selectedRace == null
                ? new List<Stage>()
                : selectedRace.Stages.OrderBy(s => s.StageNumber).ToList();

            int stageId = selectedStageId;

            if (stageId <= 0 && stages.Any())
            {
                stageId = stages.First().Id;
            }

            if (!selectedPlayerIds.Any())
            {
                selectedPlayerIds = players.Select(p => p.Id).ToList();
            }

            int totalCyclists = raceId > 0
                ? await _context.RaceEntries
                    .Where(re => re.RaceId == raceId && re.Cyclist.IsActive)
                    .Select(re => re.CyclistId)
                    .Distinct()
                    .CountAsync()
                : 0;

            return new NewGameViewModel
            {
                RaceId = raceId,
                StageId = stageId,
                SelectedPlayerIds = selectedPlayerIds,
                TotalStages = stages.Count,
                TotalCyclists = totalCyclists,

                AvailableRaces = races.Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = $"{r.Name} {r.Year} ({r.Stages.Count} ritten)",
                    Selected = r.Id == raceId
                }).ToList(),

                AvailableStages = stages.Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = $"Rit {s.StageNumber} - {s.Name}",
                    Selected = s.Id == stageId
                }).ToList(),

                AvailablePlayers = players.Select(p => new PlayerSelectItemViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    PositionInDraft = p.PositionInDraft,
                    IsSelected = selectedPlayerIds.Contains(p.Id)
                }).ToList()
            };
        }

        private static List<DraftTurn> GenerateFairSnakeDraft(
            int gameSessionId,
            int raceId,
            List<Player> players,
            int totalRounds)
        {
            var draftTurns = new List<DraftTurn>();
            int turnNumber = 1;

            for (int round = 1; round <= totalRounds; round++)
            {
                var roundPlayers = round % 2 == 1
                    ? players
                    : players.AsEnumerable().Reverse().ToList();

                foreach (var player in roundPlayers)
                {
                    draftTurns.Add(new DraftTurn
                    {
                        GameSessionId = gameSessionId,
                        RaceId = raceId,
                        PlayerId = player.Id,
                        TurnNumber = turnNumber
                    });

                    turnNumber++;
                }
            }

            return draftTurns;
        }
    }
}