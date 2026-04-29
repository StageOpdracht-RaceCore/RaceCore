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

                model.PlayersCount = await _context.DraftTurns
                    .Where(dt => dt.GameSessionId == gameId)
                    .Select(dt => dt.PlayerId)
                    .Distinct()
                    .CountAsync();

                model.TotalDraftPicks = await _context.DraftTurns
                    .Where(dt => dt.GameSessionId == gameId && dt.CyclistId != null)
                    .CountAsync();

                model.CyclistsCount = model.TotalDraftPicks;

                model.DraftCompleted = game.Status != "Draft";

                model.PlayerRanking = await _context.DraftTurns
                    .Where(dt => dt.GameSessionId == gameId)
                    .Select(dt => dt.Player)
                    .Distinct()
                    .Select(p => new PlayerRankingItem
                    {
                        PlayerName = p.Name,
                        Points = _context.PlayerPoints
                            .Where(pp => pp.PlayerId == p.Id && pp.RaceId == game.RaceId)
                            .Select(pp => (int?)pp.Points)
                            .Sum() ?? 0
                    })
                    .OrderByDescending(x => x.Points)
                    .ThenBy(x => x.PlayerName)
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
                            .Where(pp => pp.CyclistId == c.Id && pp.RaceId == game.RaceId)
                            .Select(pp => (int?)pp.Points)
                            .Sum() ?? 0
                    })
                    .OrderByDescending(x => x.Points)
                    .ThenBy(x => x.Name)
                    .Take(5)
                    .ToListAsync();

                model.Jerseys = await _context.Jerseys
                    .Include(j => j.Cyclist)
                    .Where(j => j.Stage.RaceId == game.RaceId)
                    .OrderByDescending(j => j.Stage.StageNumber)
                    .Select(j => new JerseyItem
                    {
                        Type = j.Type,
                        CyclistName = j.Cyclist.FirstName + " " + j.Cyclist.LastName
                    })
                    .Take(4)
                    .ToListAsync();

                var latestStage = await _context.Stages
                    .Where(s => s.RaceId == game.RaceId)
                    .OrderByDescending(s => s.StageNumber)
                    .FirstOrDefaultAsync();

                if (latestStage != null)
                {
                    model.LatestStageTitle = $"Stage {latestStage.StageNumber} - {latestStage.Name}";

                    model.LatestStageTop3 = await _context.StageResults
                        .Include(sr => sr.Cyclist)
                        .Where(sr => sr.StageId == latestStage.Id && sr.Position != null)
                        .OrderBy(sr => sr.Position)
                        .Take(3)
                        .Select(sr => $"{sr.Position}. {sr.Cyclist.FirstName} {sr.Cyclist.LastName}")
                        .ToListAsync();
                }

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