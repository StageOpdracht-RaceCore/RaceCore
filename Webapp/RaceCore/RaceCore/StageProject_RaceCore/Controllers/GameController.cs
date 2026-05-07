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

        public GameController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> New()
        {
            await LoadActiveGamePopupDataSafe();

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
                await LoadActiveGamePopupDataSafe();
                return View(await BuildNewGameViewModelSafe(model.RaceId, model.StageId, model.SelectedPlayerIds));
            }

            try
            {
                var race = await _context.Races
                    .FirstOrDefaultAsync(r => r.Id == model.RaceId);

                if (race == null)
                {
                    TempData["Error"] = "Race niet gevonden.";
                    await LoadActiveGamePopupDataSafe();
                    return View(await BuildNewGameViewModelSafe(model.RaceId, model.StageId, model.SelectedPlayerIds));
                }

                var stage = await _context.Stages
                    .FirstOrDefaultAsync(s => s.Id == model.StageId && s.RaceId == model.RaceId);

                if (stage == null)
                {
                    TempData["Error"] = "Rit niet gevonden bij deze race.";
                    await LoadActiveGamePopupDataSafe();
                    return View(await BuildNewGameViewModelSafe(model.RaceId, model.StageId, model.SelectedPlayerIds));
                }

                var players = await GetPlayersForNewGameDraftOrder(model.SelectedPlayerIds);

                if (players.Count < 2)
                {
                    TempData["Error"] = "Kies minstens 2 geldige spelers.";
                    await LoadActiveGamePopupDataSafe();
                    return View(await BuildNewGameViewModelSafe(model.RaceId, model.StageId, model.SelectedPlayerIds));
                }

                var game = new GameSession
                {
                    RaceId = model.RaceId,
                    StageId = model.StageId,
                    Status = "Draft",
                    CurrentStageNumber = stage.StageNumber,
                    RidersPerPlayer = 10,
                    BenchPerPlayer = 5,
                    CreatedAt = DateTime.Now
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
                await LoadActiveGamePopupDataSafe();
                return View(await BuildNewGameViewModelSafe(model.RaceId, model.StageId, model.SelectedPlayerIds));
            }
        }

        private async Task LoadActiveGamePopupDataSafe()
        {
            ViewBag.ActiveGameId = 0;
            ViewBag.ActiveGameName = "";
            ViewBag.ActiveGameStatus = "";

            try
            {
                var activeGame = await _context.GameSessions
                    .Include(g => g.Race)
                    .Where(g => g.Status == "Draft" || g.Status == "Active")
                    .OrderByDescending(g => g.CreatedAt)
                    .FirstOrDefaultAsync();

                if (activeGame == null)
                {
                    return;
                }

                ViewBag.ActiveGameId = activeGame.Id;
                ViewBag.ActiveGameStatus = activeGame.Status;

                string raceName = activeGame.Race != null
                    ? $"{activeGame.Race.Name} {activeGame.Race.Year}"
                    : "Actieve game";

                string stageName = activeGame.CurrentStageNumber > 0
                    ? $" - Rit {activeGame.CurrentStageNumber}"
                    : "";

                ViewBag.ActiveGameName = raceName + stageName;
            }
            catch
            {
                ViewBag.ActiveGameId = 0;
                ViewBag.ActiveGameName = "";
                ViewBag.ActiveGameStatus = "";
            }
        }

        private async Task<NewGameViewModel> BuildNewGameViewModelSafe(int selectedRaceId = 0, int selectedStageId = 0, List<int>? selectedPlayerIds = null)
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

        private async Task<NewGameViewModel> BuildNewGameViewModel(int selectedRaceId = 0, int selectedStageId = 0, List<int>? selectedPlayerIds = null)
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

            int totalCyclists = await _context.Cyclists
                .CountAsync(c => c.IsActive);

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

        private async Task<List<Player>> GetPlayersForNewGameDraftOrder(List<int> selectedPlayerIds)
        {
            var selectedPlayers = await _context.Players
                .Where(p => selectedPlayerIds.Contains(p.Id))
                .OrderBy(p => p.PositionInDraft)
                .ThenBy(p => p.Id)
                .ToListAsync();

            var lastFinishedGame = await _context.GameSessions
                .Where(g => g.Status != "Draft")
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastFinishedGame == null)
            {
                return selectedPlayers;
            }

            var lastRanking = await CalculatePlayerRankingForGame(lastFinishedGame.Id);

            if (!lastRanking.Any())
            {
                return selectedPlayers;
            }

            var oldPlayerIds = lastRanking
                .Select(r => r.PlayerId)
                .ToList();

            // Nieuwe spelers zaten niet in vorige leaderboard, dus die komen eerst.
            var newPlayers = selectedPlayers
                .Where(p => !oldPlayerIds.Contains(p.Id))
                .OrderBy(p => p.PositionInDraft)
                .ThenBy(p => p.Id)
                .ToList();

            // Vorige leaderboard omgekeerd: laatste wordt eerst, winnaar wordt laatst.
            var reversedLeaderboardPlayers = lastRanking
                .Where(r => selectedPlayerIds.Contains(r.PlayerId))
                .OrderBy(r => r.TotalPoints)
                .ThenBy(r => r.PlayerName)
                .Select(r => selectedPlayers.First(p => p.Id == r.PlayerId))
                .ToList();

            return newPlayers
                .Concat(reversedLeaderboardPlayers)
                .ToList();
        }

        private async Task<List<GamePlayerRankingResult>> CalculatePlayerRankingForGame(int gameId)
        {
            var game = await _context.GameSessions
                .FirstOrDefaultAsync(g => g.Id == gameId);

            if (game == null)
            {
                return new List<GamePlayerRankingResult>();
            }

            var selections = await _context.PlayerSelections
                .Where(s => s.GameSessionId == gameId)
                .Include(s => s.Player)
                .Include(s => s.Cyclist)
                .ToListAsync();

            var stages = await _context.Stages
                .Where(s => s.RaceId == game.RaceId)
                .ToListAsync();

            var stageIds = stages.Select(s => s.Id).ToList();

            var rules = await _context.PointsRules.ToListAsync();

            var allResults = await _context.StageResults
                .Where(sr => stageIds.Contains(sr.StageId))
                .ToListAsync();

            var allJerseys = await _context.Jerseys
                .Where(j => stageIds.Contains(j.StageId))
                .ToListAsync();

            var ranking = new List<GamePlayerRankingResult>();

            foreach (var playerGroup in selections.GroupBy(s => s.PlayerId))
            {
                int playerTotal = 0;
                string playerName = playerGroup.First().Player?.Name ?? "Onbekend";

                foreach (var selection in playerGroup)
                {
                    int cyclistTotal = 0;

                    foreach (var stage in stages)
                    {
                        var result = allResults.FirstOrDefault(r =>
                            r.StageId == stage.Id &&
                            r.CyclistId == selection.CyclistId);

                        if (result != null && result.Position.HasValue)
                        {
                            cyclistTotal += rules
                                .Where(r =>
                                    r.Type == "Rit" &&
                                    r.FromPosition <= result.Position.Value &&
                                    r.ToPosition >= result.Position.Value)
                                .Sum(r => r.Points);
                        }

                        var jerseys = allJerseys.Where(j =>
                            j.StageId == stage.Id &&
                            j.CyclistId == selection.CyclistId);

                        foreach (var jersey in jerseys)
                        {
                            string type = jersey.Type switch
                            {
                                "Red" => "RodeTrui",
                                "Green" => "GroeneTrui",
                                "Blue" => "BlauweTrui",
                                "White" => "WitteTrui",
                                _ => jersey.Type
                            };

                            cyclistTotal += rules
                                .Where(r => r.Type == type)
                                .Sum(r => r.Points);
                        }
                    }

                    playerTotal += cyclistTotal;
                }

                ranking.Add(new GamePlayerRankingResult
                {
                    PlayerId = playerGroup.Key,
                    PlayerName = playerName,
                    TotalPoints = playerTotal
                });
            }

            return ranking
                .OrderByDescending(r => r.TotalPoints)
                .ThenBy(r => r.PlayerName)
                .ToList();
        }

        private static List<DraftTurn> GenerateFairSnakeDraft(int gameSessionId, int raceId, List<Player> players, int totalRounds)
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

        private class GamePlayerRankingResult
        {
            public int PlayerId { get; set; }
            public string PlayerName { get; set; } = string.Empty;
            public int TotalPoints { get; set; }
        }
    }
}
