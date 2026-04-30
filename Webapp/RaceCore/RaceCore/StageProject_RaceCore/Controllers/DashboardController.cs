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
                ViewBag.RaceId = game.RaceId;
                ViewBag.GameStatus = game.Status;
                ViewBag.RaceName = game.Race.Name + " " + game.Race.Year;
                ViewBag.CurrentStage = game.CurrentStageNumber;
                ViewBag.DatabaseOnline = true;

                model.PlayersCount = await _context.DraftTurns
                    .Where(d => d.GameSessionId == gameId)
                    .Select(d => d.PlayerId)
                    .Distinct()
                    .CountAsync();

                model.TotalDraftPicks = await _context.DraftTurns
                    .Where(d => d.GameSessionId == gameId && d.CyclistId != null)
                    .CountAsync();

                model.CyclistsCount = model.TotalDraftPicks;
                model.DraftCompleted = game.Status != "Draft";

                model.PlayerRanking = await _context.DraftTurns
                    .Where(d => d.GameSessionId == gameId)
                    .Select(d => d.Player)
                    .Distinct()
                    .Select(p => new PlayerRankingItem
                    {
                        PlayerName = p.Name,
                        Points = _context.PlayerPoints
                            .Where(pp => pp.PlayerId == p.Id && pp.RaceId == game.RaceId)
                            .Select(pp => (int?)pp.Points)
                            .Sum() ?? 0
                    })
                    .OrderByDescending(p => p.Points)
                    .ThenBy(p => p.PlayerName)
                    .ToListAsync();

                for (int i = 0; i < model.PlayerRanking.Count; i++)
                {
                    model.PlayerRanking[i].Position = i + 1;
                }

                model.TopCyclists = await _context.PlayerSelections
                    .Where(s => s.GameSessionId == gameId)
                    .Select(s => s.Cyclist)
                    .Distinct()
                    .Select(c => new TopCyclistItem
                    {
                        Name = c.FirstName + " " + c.LastName,
                        Points = _context.PlayerPoints
                            .Where(pp => pp.CyclistId == c.Id && pp.RaceId == game.RaceId)
                            .Select(pp => (int?)pp.Points)
                            .Sum() ?? 0
                    })
                    .OrderByDescending(c => c.Points)
                    .ThenBy(c => c.Name)
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
                    model.LatestStageTitle = "Stage " + latestStage.StageNumber + " - " + latestStage.Name;

                    model.LatestStageTop3 = await _context.StageResults
                        .Include(r => r.Cyclist)
                        .Where(r => r.StageId == latestStage.Id && r.Position != null)
                        .OrderBy(r => r.Position)
                        .Take(3)
                        .Select(r => r.Position + ". " + r.Cyclist.FirstName + " " + r.Cyclist.LastName)
                        .ToListAsync();
                }
            }
            catch
            {
                ViewBag.DatabaseOnline = false;
            }

            return View(model);
        }
    }
}