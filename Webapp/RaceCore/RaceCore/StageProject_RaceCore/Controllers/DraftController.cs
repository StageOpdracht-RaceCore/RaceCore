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
            ViewBag.Cyclists = new List<Cyclist>();
            ViewBag.GameId = gameId;
            ViewBag.RaceId = 0;
            ViewBag.DatabaseOnline = false;
            ViewBag.NoDraft = false;
            ViewBag.PlayerCount = 0;
            ViewBag.GameStatus = "Unknown";

            try
            {
                if (gameId <= 0)
                {
                    var latestGame = await _context.GameSessions
                        .Include(g => g.Race)
                        .OrderByDescending(g => g.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (latestGame == null)
                    {
                        TempData["Error"] = "Er is nog geen game gestart. Start eerst een nieuwe game.";
                        return RedirectToAction("New", "Game");
                    }

                    gameId = latestGame.Id;
                }

                var game = await _context.GameSessions
                    .Include(g => g.Race)
                    .FirstOrDefaultAsync(g => g.Id == gameId);

                if (game == null)
                {
                    TempData["Error"] = "Game niet gevonden.";
                    return RedirectToAction("New", "Game");
                }

                var draftTurnsDb = await _context.DraftTurns
                    .Where(d => d.GameSessionId == game.Id)
                    .Include(d => d.Player)
                    .Include(d => d.Cyclist)
                    .OrderBy(d => d.TurnNumber)
                    .ToListAsync();

                var playerIdsInDraft = draftTurnsDb
                    .Select(d => d.PlayerId)
                    .Distinct()
                    .ToList();

                var pickedCyclistIds = draftTurnsDb
                    .Where(d => d.CyclistId != null)
                    .Select(d => d.CyclistId!.Value)
                    .ToList();

                var cyclists = await _context.Cyclists
                    .Where(c => c.IsActive && !pickedCyclistIds.Contains(c.Id))
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .ToListAsync();

                var viewModel = draftTurnsDb.Select(d => new DraftTurnViewModel
                {
                    Id = d.Id,
                    TurnNumber = d.TurnNumber,
                    PlayerId = d.PlayerId,
                    PlayerName = d.Player != null ? d.Player.Name : "Unknown",
                    CyclistId = d.CyclistId,
                    CyclistName = d.Cyclist != null ? d.Cyclist.FullName : null
                }).ToList();

                ViewBag.Cyclists = cyclists;
                ViewBag.GameId = game.Id;
                ViewBag.RaceId = game.RaceId;
                ViewBag.RaceName = game.Race != null ? $"{game.Race.Name} {game.Race.Year}" : "Onbekende race";
                ViewBag.DatabaseOnline = true;
                ViewBag.NoDraft = !draftTurnsDb.Any();
                ViewBag.PlayerCount = playerIdsInDraft.Count;
                ViewBag.GameStatus = game.Status;
                ViewBag.RidersPerPlayer = game.RidersPerPlayer;
                ViewBag.BenchPerPlayer = game.BenchPerPlayer;

                return View(viewModel);
            }
            catch
            {
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
                if (gameId <= 0)
                {
                    TempData["Error"] = "Geen geldige game gevonden.";
                    return RedirectToAction("New", "Game");
                }

                var game = await _context.GameSessions
                    .FirstOrDefaultAsync(g => g.Id == gameId);

                if (game == null)
                {
                    TempData["Error"] = "Game niet gevonden.";
                    return RedirectToAction("New", "Game");
                }

                if (game.Status != "Draft")
                {
                    TempData["Error"] = "Deze draft is niet meer actief.";
                    return RedirectToDraftPlayersSection(gameId);
                }

                var currentTurn = await _context.DraftTurns
                    .Include(t => t.Player)
                    .Where(t => t.GameSessionId == gameId && t.CyclistId == null)
                    .OrderBy(t => t.TurnNumber)
                    .FirstOrDefaultAsync();

                if (currentTurn == null)
                {
                    game.Status = "Active";
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "De draft is afgerond. De game is nu actief.";
                    return RedirectToAction("Index", "Dashboard", new { gameId });
                }

                if (currentTurn.Id != draftTurnId)
                {
                    TempData["Error"] = "Je kan alleen kiezen voor de huidige beurt.";
                    return RedirectToDraftPlayersSection(gameId);
                }

                var cyclist = await _context.Cyclists
                    .FirstOrDefaultAsync(c => c.Id == cyclistId && c.IsActive);

                if (cyclist == null)
                {
                    TempData["Error"] = "Kies eerst een geldige actieve renner.";
                    return RedirectToDraftPlayersSection(gameId);
                }

                bool alreadyPicked = await _context.DraftTurns
                    .AnyAsync(t => t.GameSessionId == gameId && t.CyclistId == cyclistId);

                if (alreadyPicked)
                {
                    TempData["Error"] = "Deze renner is al gekozen.";
                    return RedirectToDraftPlayersSection(gameId);
                }

                currentTurn.CyclistId = cyclistId;

                int currentPlayerPickCount = await _context.PlayerSelections
                    .CountAsync(ps => ps.GameSessionId == gameId && ps.PlayerId == currentTurn.PlayerId);

                bool isActive = currentPlayerPickCount < game.RidersPerPlayer;

                _context.PlayerSelections.Add(new PlayerSelection
                {
                    GameSessionId = gameId,
                    RaceId = game.RaceId,
                    PlayerId = currentTurn.PlayerId,
                    CyclistId = cyclistId,
                    IsActive = isActive
                });

                bool draftFinished = !await _context.DraftTurns
                    .AnyAsync(t => t.GameSessionId == gameId && t.CyclistId == null && t.Id != currentTurn.Id);

                if (draftFinished)
                {
                    game.Status = "Active";
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"{currentTurn.Player.Name} heeft {cyclist.FullName} gekozen.";

                if (draftFinished)
                {
                    TempData["Success"] = "Draft afgerond. De game is nu actief.";
                    return RedirectToAction("Index", "Dashboard", new { gameId });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Pick fout: " + ex.Message;
            }

            return RedirectToDraftPlayersSection(gameId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GenerateDraft(int gameId)
        {
            TempData["Error"] = "Drafts worden nu automatisch aangemaakt via New Game.";
            return RedirectToDraftPlayersSection(gameId);
        }

        private IActionResult RedirectToDraftPlayersSection(int gameId)
        {
            return Redirect(Url.Action("Index", "Draft", new { gameId }) + "#draft-players-section");
        }
    }
}