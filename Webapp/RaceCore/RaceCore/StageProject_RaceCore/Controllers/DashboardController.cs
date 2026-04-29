using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int gameId)
        {
            var model = new DashboardViewModel();

            try
            {
                if (gameId <= 0)
                {
                    var latestGame = await _context.GameSessions
                        .OrderByDescending(g => g.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (latestGame == null)
                    {
                        TempData["Error"] = "Start eerst een game.";
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

                ViewBag.GameId = game.Id;
                ViewBag.GameStatus = game.Status;
                ViewBag.RaceName = $"{game.Race.Name} {game.Race.Year}";
                ViewBag.CurrentStage = game.CurrentStageNumber;

                model.PlayersCount = await _context.PlayerSelections
                    .Where(ps => ps.GameSessionId == gameId)
                    .Select(ps => ps.PlayerId)
                    .Distinct()
                    .CountAsync();

                model.CyclistsCount = await _context.PlayerSelections
                    .Where(ps => ps.GameSessionId == gameId)
                    .CountAsync();

                model.PlayerRanking = await _context.PlayerSelections
                    .Where(ps => ps.GameSessionId == gameId)
                    .GroupBy(ps => ps.Player.Name)
                    .Select(g => new PlayerRankingItem
                    {
                        PlayerName = g.Key,
                        Points = _context.PlayerPoints
                            .Where(pp => pp.PlayerId == g.First().PlayerId)
                            .Select(pp => (int?)pp.Points)
                            .Sum() ?? 0
                    })
                    .OrderByDescending(x => x.Points)
                    .ToListAsync();

                for (int i = 0; i < model.PlayerRanking.Count; i++)
                {
                    model.PlayerRanking[i].Position = i + 1;
                }

                model.TopCyclists = await _context.PlayerSelections
                    .Where(ps => ps.GameSessionId == gameId)
                    .Select(ps => ps.Cyclist)
                    .Distinct()
                    .Select(c => new TopCyclistItem
                    {
                        Name = c.FirstName + " " + c.LastName,
                        Points = _context.PlayerPoints
                            .Where(pp => pp.CyclistId == c.Id)
                            .Select(pp => (int?)pp.Points)
                            .Sum() ?? 0
                    })
                    .OrderByDescending(x => x.Points)
                    .Take(5)
                    .ToListAsync();

                model.DraftCompleted = game.Status != "Draft";

                model.TotalDraftPicks = await _context.DraftTurns
                    .Where(dt => dt.GameSessionId == gameId && dt.CyclistId != null)
                    .CountAsync();

                ViewBag.DatabaseOnline = true;
            }
            catch
            {
                ViewBag.DatabaseOnline = false;
            }

            return View(model);
        }
    }
}