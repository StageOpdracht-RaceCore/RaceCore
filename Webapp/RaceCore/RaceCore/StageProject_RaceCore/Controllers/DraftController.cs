using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class DraftController : Controller
    {
        private readonly AppDbContext _context;

        public DraftController(AppDbContext context)
        {
            _context = context;
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
                        TempData["Error"] = "Start eerst een nieuwe game.";
                        return RedirectToAction("New", "Game");
                    }

                    gameId = lastGame.Id;
                }

                var game = await _context.GameSessions
                    .Include(g => g.Race)
                    .FirstOrDefaultAsync(g => g.Id == gameId);

                if (game == null)
                {
                    TempData["Error"] = "Game niet gevonden.";
                    return RedirectToAction("New", "Game");
                }

                var draftTurns = await _context.DraftTurns
                    .Include(d => d.Player)
                    .Include(d => d.Cyclist)
                    .Where(d => d.GameSessionId == gameId)
                    .OrderBy(d => d.TurnNumber)
                    .ToListAsync();

                var pickedCyclistIds = draftTurns
                    .Where(d => d.CyclistId != null)
                    .Select(d => d.CyclistId.Value)
                    .ToList();

                var cyclists = await _context.Cyclists
                    .Where(c => c.IsActive && !pickedCyclistIds.Contains(c.Id))
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .ToListAsync();

                var model = new List<DraftTurnViewModel>();

                foreach (var turn in draftTurns)
                {
                    model.Add(new DraftTurnViewModel
                    {
                        Id = turn.Id,
                        TurnNumber = turn.TurnNumber,
                        PlayerId = turn.PlayerId,
                        PlayerName = turn.Player.Name,
                        CyclistId = turn.CyclistId,
                        CyclistName = turn.Cyclist != null ? turn.Cyclist.FullName : null
                    });
                }

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
                TempData["DatabaseError"] = "Database niet bereikbaar.";
                return View(new List<DraftTurnViewModel>());
            }
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
                    TempData["Error"] = "Game niet gevonden.";
                    return RedirectToAction("New", "Game");
                }

                if (game.Status != "Draft")
                {
                    TempData["Error"] = "Deze draft is al klaar.";
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

                    return RedirectToAction("Index", "Dashboard", new { gameId });
                }

                if (currentTurn.Id != draftTurnId)
                {
                    TempData["Error"] = "Dit is niet de huidige beurt.";
                    return RedirectToDraft(gameId);
                }

                var cyclist = await _context.Cyclists
                    .FirstOrDefaultAsync(c => c.Id == cyclistId && c.IsActive);

                if (cyclist == null)
                {
                    TempData["Error"] = "Renner niet gevonden.";
                    return RedirectToDraft(gameId);
                }

                bool cyclistAlreadyPicked = await _context.DraftTurns
                    .AnyAsync(d => d.GameSessionId == gameId && d.CyclistId == cyclistId);

                if (cyclistAlreadyPicked)
                {
                    TempData["Error"] = "Deze renner is al gekozen.";
                    return RedirectToDraft(gameId);
                }

                currentTurn.CyclistId = cyclistId;

                int playerPickCount = await _context.PlayerSelections
                    .CountAsync(s => s.GameSessionId == gameId && s.PlayerId == currentTurn.PlayerId);

                bool isActiveCyclist = playerPickCount < game.RidersPerPlayer;

                var selection = new PlayerSelection
                {
                    GameSessionId = gameId,
                    RaceId = game.RaceId,
                    PlayerId = currentTurn.PlayerId,
                    CyclistId = cyclistId,
                    IsActive = isActiveCyclist
                };

                _context.PlayerSelections.Add(selection);

                bool draftIsFinished = !await _context.DraftTurns
                    .AnyAsync(d => d.GameSessionId == gameId &&
                                   d.CyclistId == null &&
                                   d.Id != currentTurn.Id);

                if (draftIsFinished)
                {
                    game.Status = "Active";
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = currentTurn.Player.Name + " heeft " + cyclist.FullName + " gekozen.";

                if (draftIsFinished)
                {
                    return RedirectToAction("Index", "Dashboard", new { gameId });
                }

                return RedirectToDraft(gameId);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Pick fout: " + ex.Message;
                return RedirectToDraft(gameId);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GenerateDraft(int gameId)
        {
            TempData["Error"] = "Draft wordt automatisch gemaakt via New Game.";
            return RedirectToDraft(gameId);
        }

        private IActionResult RedirectToDraft(int gameId)
        {
            return RedirectToAction("Index", "Draft", new { gameId });
        }
    }
}