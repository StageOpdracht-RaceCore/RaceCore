using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class GameController : Controller
    {
        private readonly AppDbContext _context;

        private const int HostTimeoutSeconds = 60;

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
                ModelState.AddModelError(nameof(model.RaceId), "Choose a race.");
            }

            if (model.SelectedPlayerIds.Count < 2)
            {
                ModelState.AddModelError(nameof(model.SelectedPlayerIds), "Choose at least 2 players.");
            }

            if (model.RidersPerPlayer < 1)
            {
                ModelState.AddModelError(nameof(model.RidersPerPlayer), "Active riders must be at least 1.");
            }

            if (model.BenchPerPlayer < 0)
            {
                ModelState.AddModelError(nameof(model.BenchPerPlayer), "Bench riders cannot be negative.");
            }

            int totalPicksPerPlayer = model.RidersPerPlayer + model.BenchPerPlayer;

            if (totalPicksPerPlayer <= 0)
            {
                ModelState.AddModelError("", "Total amount of picks must be greater than 0.");
            }

            if (!ModelState.IsValid)
            {
                await SetActiveGameViewBag();

                return View(await BuildNewGameViewModelSafe(
                    model.RaceId,
                    model.SelectedPlayerIds,
                    model.RidersPerPlayer,
                    model.BenchPerPlayer));
            }

            try
            {
                var race = await _context.Races
                    .Include(r => r.Stages)
                    .FirstOrDefaultAsync(r => r.Id == model.RaceId);

                if (race == null)
                {
                    TempData["Error"] = "Race not found.";
                    await SetActiveGameViewBag();

                    return View(await BuildNewGameViewModelSafe(
                        model.RaceId,
                        model.SelectedPlayerIds,
                        model.RidersPerPlayer,
                        model.BenchPerPlayer));
                }

                var players = await _context.Players
                    .Where(p => model.SelectedPlayerIds.Contains(p.Id))
                    .OrderBy(p => p.PositionInDraft)
                    .ThenBy(p => p.Id)
                    .ToListAsync();

                if (players.Count < 2)
                {
                    TempData["Error"] = "Choose at least 2 valid players.";
                    await SetActiveGameViewBag();

                    return View(await BuildNewGameViewModelSafe(
                        model.RaceId,
                        model.SelectedPlayerIds,
                        model.RidersPerPlayer,
                        model.BenchPerPlayer));
                }

                int availableCyclistsCount = await _context.RaceEntries
                    .Where(re => re.RaceId == model.RaceId && re.Cyclist.IsActive)
                    .Select(re => re.CyclistId)
                    .Distinct()
                    .CountAsync();

                int neededCyclistsCount = players.Count * totalPicksPerPlayer;

                if (availableCyclistsCount < neededCyclistsCount)
                {
                    TempData["Error"] =
                        $"Not enough cyclists for this draft. Needed: {neededCyclistsCount}, available in this race: {availableCyclistsCount}.";

                    await SetActiveGameViewBag();

                    return View(await BuildNewGameViewModelSafe(
                        model.RaceId,
                        model.SelectedPlayerIds,
                        model.RidersPerPlayer,
                        model.BenchPerPlayer));
                }

                string hostSessionId = GetOrCreateHostSessionId();

                var game = new GameSession
                {
                    RaceId = model.RaceId,
                    Status = "Draft",
                    CurrentStageNumber = 0,
                    RidersPerPlayer = model.RidersPerPlayer,
                    BenchPerPlayer = model.BenchPerPlayer,
                    CreatedAt = DateTime.Now,
                    HostSessionId = hostSessionId,
                    LastHostPingAt = DateTime.Now
                };

                _context.GameSessions.Add(game);
                await _context.SaveChangesAsync();

                var draftTurns = GenerateFairSnakeDraft(
                    game.Id,
                    race.Id,
                    players,
                    totalPicksPerPlayer);

                _context.DraftTurns.AddRange(draftTurns);
                await _context.SaveChangesAsync();

                TempData["Success"] =
                    $"Game {race.Name} {race.Year} has started. " +
                    $"Draft: {model.RidersPerPlayer} active + {model.BenchPerPlayer} bench.";

                return RedirectToAction("Index", "Draft", new { gameId = game.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Start Game error: " + ex.Message;
                await SetActiveGameViewBag();

                return View(await BuildNewGameViewModelSafe(
                    model.RaceId,
                    model.SelectedPlayerIds,
                    model.RidersPerPlayer,
                    model.BenchPerPlayer));
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

            ViewBag.ActiveGameStageName = activeGame?.Race != null
                ? "All stages of this race"
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
            List<int>? selectedPlayerIds = null,
            int ridersPerPlayer = 12,
            int benchPerPlayer = 6)
        {
            try
            {
                return await BuildNewGameViewModel(
                    selectedRaceId,
                    selectedPlayerIds,
                    ridersPerPlayer,
                    benchPerPlayer);
            }
            catch
            {
                TempData["DatabaseError"] = "Database unavailable. Start OpenVPN to load races and players.";

                return new NewGameViewModel
                {
                    RaceId = selectedRaceId,
                    SelectedPlayerIds = selectedPlayerIds ?? new List<int>(),
                    RidersPerPlayer = ridersPerPlayer,
                    BenchPerPlayer = benchPerPlayer,
                    AvailableRaces = new List<SelectListItem>(),
                    AvailablePlayers = new List<PlayerSelectItemViewModel>(),
                    TotalStages = 0,
                    TotalCyclists = 0,
                    AvailableRaceCyclists = 0
                };
            }
        }

        private async Task<NewGameViewModel> BuildNewGameViewModel(
            int selectedRaceId = 0,
            List<int>? selectedPlayerIds = null,
            int ridersPerPlayer = 12,
            int benchPerPlayer = 6)
        {
            selectedPlayerIds ??= new List<int>();

            var racesFromDatabase = await _context.Races
                .Include(r => r.Stages)
                .ToListAsync();

            var races = racesFromDatabase
                .OrderBy(r => GetRaceCategoryOrder(r))
                .ThenByDescending(r => r.Year)
                .ThenBy(r => r.Name)
                .ToList();

            var players = await _context.Players
                .OrderBy(p => p.PositionInDraft)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var selectedRace = selectedRaceId > 0
                ? races.FirstOrDefault(r => r.Id == selectedRaceId)
                : races.FirstOrDefault();

            int raceId = selectedRace?.Id ?? 0;

            int totalStages = selectedRace?.Stages?.Count ?? 0;

            if (!selectedPlayerIds.Any())
            {
                selectedPlayerIds = players.Select(p => p.Id).ToList();
            }

            int totalCyclists = await _context.Cyclists.CountAsync();

            int availableRaceCyclists = raceId > 0
                ? await _context.RaceEntries
                    .Where(re => re.RaceId == raceId && re.Cyclist.IsActive)
                    .Select(re => re.CyclistId)
                    .Distinct()
                    .CountAsync()
                : 0;

            var groups = new Dictionary<string, SelectListGroup>();

            foreach (var category in GetRaceCategoryOrderList())
            {
                groups[category] = new SelectListGroup
                {
                    Name = category
                };
            }

            return new NewGameViewModel
            {
                RaceId = raceId,
                SelectedPlayerIds = selectedPlayerIds,
                RidersPerPlayer = ridersPerPlayer,
                BenchPerPlayer = benchPerPlayer,
                TotalStages = totalStages,
                TotalCyclists = totalCyclists,
                AvailableRaceCyclists = availableRaceCyclists,

                AvailableRaces = races.Select(r =>
                {
                    string category = GetRaceCategory(r);

                    if (!groups.ContainsKey(category))
                    {
                        groups[category] = new SelectListGroup
                        {
                            Name = category
                        };
                    }

                    return new SelectListItem
                    {
                        Value = r.Id.ToString(),
                        Text = $"{r.Name} {r.Year} ({r.Stages.Count} stages)",
                        Selected = r.Id == raceId,
                        Group = groups[category]
                    };
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

        private static string GetRaceCategory(Race race)
        {
            if (!string.IsNullOrWhiteSpace(race.Category) && race.Category != "Andere")
            {
                return race.Category;
            }

            string name = race.Name.ToLower();

            if (name.Contains("tour de france") ||
                name.Contains("giro") ||
                name.Contains("vuelta"))
            {
                return "Grand Tours";
            }

            if (name.Contains("milano") ||
                name.Contains("sanremo") ||
                name.Contains("ronde van vlaanderen") ||
                name.Contains("paris-roubaix") ||
                name.Contains("roubaix") ||
                name.Contains("liège") ||
                name.Contains("liege") ||
                name.Contains("bastogne") ||
                name.Contains("lombardia"))
            {
                return "Monuments";
            }

            if (name.Contains("omloop") ||
                name.Contains("strade") ||
                name.Contains("e3") ||
                name.Contains("gent-wevelgem") ||
                name.Contains("dwars door vlaanderen") ||
                name.Contains("amstel") ||
                name.Contains("flèche") ||
                name.Contains("fleche") ||
                name.Contains("kuurne") ||
                name.Contains("nokere") ||
                name.Contains("bredene") ||
                name.Contains("koksijde"))
            {
                return "Spring Classics";
            }

            if (name.Contains("paris-nice") ||
                name.Contains("tirreno") ||
                name.Contains("dauphiné") ||
                name.Contains("dauphine") ||
                name.Contains("romandie") ||
                name.Contains("basque") ||
                name.Contains("catalunya") ||
                name.Contains("benelux"))
            {
                return "Small Tours";
            }

            if (race.Stages != null && race.Stages.Count > 1)
            {
                return "Small Tours";
            }

            return "One Day Races";
        }

        private static int GetRaceCategoryOrder(Race race)
        {
            return GetRaceCategory(race) switch
            {
                "Grand Tours" => 1,
                "Small Tours" => 2,
                "Monuments" => 3,
                "Spring Classics" => 4,
                "One Day Races" => 5,
                _ => 99
            };
        }

        private static List<string> GetRaceCategoryOrderList()
        {
            return new List<string>
            {
                "Grand Tours",
                "Small Tours",
                "Monuments",
                "Spring Classics",
                "One Day Races",
                "Other"
            };
        }
    }
}