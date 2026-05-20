using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Hubs;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class DraftController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<GameHub> _hubContext;

        public DraftController(AppDbContext context, IHubContext<GameHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index(int gameId)
        {
            try
            {
                if (gameId <= 0)
                {
                    var lastGame = await _context.GameSessions
                        .OrderByDescending(g => g.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (lastGame == null)
                    {
                        TempData["Error"] = "Start a new game first.";
                        return RedirectToAction("New", "Game");
                    }

                    gameId = lastGame.Id;
                }

                var game = await _context.GameSessions
                    .Include(g => g.Race)
                    .FirstOrDefaultAsync(g => g.Id == gameId);

                if (game == null)
                {
                    TempData["Error"] = "Game not found.";
                    return RedirectToAction("New", "Game");
                }

                await FixDraftTurnsIfNeeded(game);

                var draftTurns = await _context.DraftTurns
                    .Include(d => d.Player)
                    .Include(d => d.Cyclist)
                    .Where(d => d.GameSessionId == gameId)
                    .OrderBy(d => d.TurnNumber)
                    .ToListAsync();

                await FixPlayerSelectionActiveStatus(game, draftTurns);

                var pickedCyclistIds = draftTurns
                    .Where(d => d.CyclistId != null)
                    .Select(d => d.CyclistId!.Value)
                    .ToList();

                var raceCyclistIds = await _context.RaceEntries
                    .Where(re => re.RaceId == game.RaceId && re.Cyclist.IsActive)
                    .Select(re => re.CyclistId)
                    .Distinct()
                    .ToListAsync();

                var cyclists = await _context.Cyclists
                    .Include(c => c.Team)
                    .Where(c =>
                        c.IsActive &&
                        raceCyclistIds.Contains(c.Id) &&
                        !pickedCyclistIds.Contains(c.Id))
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .ToListAsync();

                var model = draftTurns.Select(turn => new DraftTurnViewModel
                {
                    Id = turn.Id,
                    TurnNumber = turn.TurnNumber,
                    PlayerId = turn.PlayerId,
                    PlayerName = turn.Player.Name,
                    CyclistId = turn.CyclistId,
                    CyclistName = turn.Cyclist != null ? turn.Cyclist.FullName : null
                }).ToList();

                ViewBag.GameId = game.Id;
                ViewBag.RaceId = game.RaceId;
                ViewBag.RaceName = game.Race.Name + " " + game.Race.Year;
                ViewBag.GameStatus = game.Status;
                ViewBag.Cyclists = cyclists;
                ViewBag.DatabaseOnline = true;
                ViewBag.PlayerCount = draftTurns.Select(d => d.PlayerId).Distinct().Count();
                ViewBag.NoDraft = !draftTurns.Any();
                ViewBag.RidersPerPlayer = game.RidersPerPlayer;
                ViewBag.BenchPerPlayer = game.BenchPerPlayer;

                return View(model);
            }
            catch
            {
                ViewBag.DatabaseOnline = false;
                TempData["DatabaseError"] = "Database unavailable.";
                return View(new List<DraftTurnViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDraftState(int gameId)
        {
            var game = await _context.GameSessions
                .FirstOrDefaultAsync(g => g.Id == gameId);

            if (game == null)
            {
                return Json(new { exists = false });
            }

            int pickedCount = await _context.DraftTurns
                .Where(d => d.GameSessionId == gameId && d.CyclistId != null)
                .CountAsync();

            int totalTurns = await _context.DraftTurns
                .Where(d => d.GameSessionId == gameId)
                .CountAsync();

            int currentTurnId = await _context.DraftTurns
                .Where(d => d.GameSessionId == gameId && d.CyclistId == null)
                .OrderBy(d => d.TurnNumber)
                .Select(d => d.Id)
                .FirstOrDefaultAsync();

            return Json(new
            {
                exists = true,
                gameId = gameId,
                status = game.Status,
                pickedCount = pickedCount,
                totalTurns = totalTurns,
                currentTurnId = currentTurnId,
                ridersPerPlayer = game.RidersPerPlayer,
                benchPerPlayer = game.BenchPerPlayer,
                version = pickedCount + "-" + currentTurnId + "-" + game.Status + "-" + game.RidersPerPlayer + "-" + game.BenchPerPlayer
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PickCyclist(int draftTurnId, int cyclistId, int gameId)
        {
            try
            {
                var game = await _context.GameSessions
                    .FirstOrDefaultAsync(g => g.Id == gameId);

                if (game == null)
                {
                    TempData["Error"] = "Game not found.";
                    return RedirectToAction("New", "Game");
                }

                if (game.Status != "Draft")
                {
                    TempData["Error"] = "This draft is already finished.";
                    return RedirectToDraft(gameId);
                }

                var currentTurn = await _context.DraftTurns
                    .Include(d => d.Player)
                    .Where(d => d.GameSessionId == gameId && d.CyclistId == null)
                    .OrderBy(d => d.TurnNumber)
                    .FirstOrDefaultAsync();

                if (currentTurn == null)
                {
                    game.Status = "Active";
                    await _context.SaveChangesAsync();
                    await SendDraftUpdate(gameId, true);
                    return RedirectToAction("Index", "Dashboard", new { gameId });
                }

                if (currentTurn.Id != draftTurnId)
                {
                    TempData["Error"] = "This is not the current turn.";
                    return RedirectToDraft(gameId);
                }

                var cyclist = await _context.RaceEntries
                    .Where(re =>
                        re.RaceId == game.RaceId &&
                        re.CyclistId == cyclistId &&
                        re.Cyclist.IsActive)
                    .Select(re => re.Cyclist)
                    .FirstOrDefaultAsync();

                if (cyclist == null)
                {
                    TempData["Error"] = "This cyclist does not belong to the selected race.";
                    return RedirectToDraft(gameId);
                }

                bool cyclistAlreadyPicked = await _context.DraftTurns
                    .AnyAsync(d => d.GameSessionId == gameId && d.CyclistId == cyclistId);

                if (cyclistAlreadyPicked)
                {
                    TempData["Error"] = "This cyclist has already been picked.";
                    return RedirectToDraft(gameId);
                }

                currentTurn.CyclistId = cyclistId;

                int playerPickCount = await _context.PlayerSelections
                    .CountAsync(s => s.GameSessionId == gameId && s.PlayerId == currentTurn.PlayerId);

                bool isActiveCyclist = playerPickCount < game.RidersPerPlayer;

                _context.PlayerSelections.Add(new PlayerSelection
                {
                    GameSessionId = gameId,
                    RaceId = game.RaceId,
                    PlayerId = currentTurn.PlayerId,
                    CyclistId = cyclistId,
                    IsActive = isActiveCyclist
                });

                bool draftIsFinished = !await _context.DraftTurns
                    .AnyAsync(d =>
                        d.GameSessionId == gameId &&
                        d.CyclistId == null &&
                        d.Id != currentTurn.Id);

                if (draftIsFinished)
                {
                    game.Status = "Active";
                }

                await _context.SaveChangesAsync();
                await SendDraftUpdate(gameId, draftIsFinished);

                TempData["Success"] = isActiveCyclist
                    ? currentTurn.Player.Name + " has picked active rider " + cyclist.FullName + "."
                    : currentTurn.Player.Name + " has picked bench rider " + cyclist.FullName + ".";

                if (draftIsFinished)
                {
                    return RedirectToAction("Index", "Dashboard", new { gameId });
                }

                return RedirectToDraft(gameId);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Pick error: " + ex.Message;
                return RedirectToDraft(gameId);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GenerateDraft(int gameId)
        {
            TempData["Error"] = "Draft is automatically generated via New Game.";
            return RedirectToDraft(gameId);
        }

        private async Task SendDraftUpdate(int gameId, bool draftFinished)
        {
            await _hubContext.Clients.All.SendAsync("DraftUpdated", new
            {
                gameId = gameId,
                draftFinished = draftFinished,
                updatedAt = DateTime.Now.Ticks
            });
        }

        private async Task FixDraftTurnsIfNeeded(GameSession game)
        {
            if (game.RidersPerPlayer <= 0)
            {
                game.RidersPerPlayer = 12;
            }

            if (game.BenchPerPlayer < 0)
            {
                game.BenchPerPlayer = 6;
            }

            var draftTurns = await _context.DraftTurns
                .Where(d => d.GameSessionId == game.Id)
                .OrderBy(d => d.TurnNumber)
                .ToListAsync();

            if (!draftTurns.Any())
            {
                await _context.SaveChangesAsync();
                return;
            }

            var playerOrder = draftTurns
                .OrderBy(d => d.TurnNumber)
                .Select(d => d.PlayerId)
                .Distinct()
                .ToList();

            int playerCount = playerOrder.Count;

            if (playerCount <= 0)
            {
                await _context.SaveChangesAsync();
                return;
            }

            int correctRounds = game.RidersPerPlayer + game.BenchPerPlayer;
            int correctTotalTurns = playerCount * correctRounds;

            if (draftTurns.Count == correctTotalTurns)
            {
                await _context.SaveChangesAsync();
                return;
            }

            if (draftTurns.Count > correctTotalTurns)
            {
                var removableTurns = draftTurns
                    .Where(d => d.CyclistId == null)
                    .OrderByDescending(d => d.TurnNumber)
                    .Take(draftTurns.Count - correctTotalTurns)
                    .ToList();

                if (removableTurns.Count == draftTurns.Count - correctTotalTurns)
                {
                    _context.DraftTurns.RemoveRange(removableTurns);
                    await _context.SaveChangesAsync();
                }

                return;
            }

            int nextTurnNumber = draftTurns.Max(d => d.TurnNumber) + 1;
            int existingRounds = draftTurns.Count / playerCount;

            for (int roundIndex = existingRounds; roundIndex < correctRounds; roundIndex++)
            {
                List<int> roundPlayers = roundIndex % 2 == 0
                    ? playerOrder
                    : playerOrder.AsEnumerable().Reverse().ToList();

                foreach (var playerId in roundPlayers)
                {
                    _context.DraftTurns.Add(new DraftTurn
                    {
                        GameSessionId = game.Id,
                        RaceId = game.RaceId,
                        PlayerId = playerId,
                        TurnNumber = nextTurnNumber
                    });

                    nextTurnNumber++;
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task FixPlayerSelectionActiveStatus(GameSession game, List<DraftTurn> draftTurns)
        {
            var selections = await _context.PlayerSelections
                .Where(s => s.GameSessionId == game.Id)
                .ToListAsync();

            var playerGroups = draftTurns
                .Where(d => d.CyclistId != null)
                .GroupBy(d => d.PlayerId);

            foreach (var playerGroup in playerGroups)
            {
                var orderedPicks = playerGroup.OrderBy(d => d.TurnNumber).ToList();

                for (int i = 0; i < orderedPicks.Count; i++)
                {
                    var pick = orderedPicks[i];

                    var selection = selections.FirstOrDefault(s =>
                        s.PlayerId == pick.PlayerId &&
                        s.CyclistId == pick.CyclistId);

                    if (selection != null)
                    {
                        selection.IsActive = i < game.RidersPerPlayer;
                        selection.RaceId = game.RaceId;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        private IActionResult RedirectToDraft(int gameId)
        {
            return Redirect("/Draft/Index?gameId=" + gameId + "#draft-players-section");
        }
    }
}