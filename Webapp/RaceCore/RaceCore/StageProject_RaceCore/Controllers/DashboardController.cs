using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    /* DashboardController.cs
       Purpose: Compose dashboard view model that aggregates game,
       stage and player data. This controller performs read-only
       queries to compute rankings, top cyclists and player stats.
    */
    /// <summary>
    /// Controller for the main game dashboard.
    /// </summary>
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
                model.TeamsCount = await _context.Teams.CountAsync();

                model.StagesCount = await _context.Stages
                    .Where(s => s.RaceId == game.RaceId)
                    .CountAsync();

                model.DraftCompleted = game.Status != "Draft";

                var rules = await _context.PointsRules.ToListAsync();

                var stageResults = await _context.StageResults
                    .Include(sr => sr.Stage)
                    .Include(sr => sr.Cyclist)
                    .Where(sr => sr.Stage.RaceId == game.RaceId)
                    .ToListAsync();

                var jerseys = await _context.Jerseys
                    .Include(j => j.Stage)
                    .Include(j => j.Cyclist)
                    .Where(j => j.Stage.RaceId == game.RaceId)
                    .ToListAsync();

                var cyclistPoints = new Dictionary<int, int>();

                foreach (var result in stageResults)
                {
                    if (result.Position == null)
                    {
                        continue;
                    }

                    int points = rules
                        .Where(r =>
                            r.Type == "Rit" &&
                            r.FromPosition <= result.Position &&
                            r.ToPosition >= result.Position)
                        .Sum(r => r.Points);

                    if (!cyclistPoints.ContainsKey(result.CyclistId))
                    {
                        cyclistPoints[result.CyclistId] = 0;
                    }

                    cyclistPoints[result.CyclistId] += points;
                }

                foreach (var jersey in jerseys)
                {
                    int points = rules
                        .Where(r => r.Type == GetRuleTypeForJersey(jersey.Type))
                        .Sum(r => r.Points);

                    if (!cyclistPoints.ContainsKey(jersey.CyclistId))
                    {
                        cyclistPoints[jersey.CyclistId] = 0;
                    }

                    cyclistPoints[jersey.CyclistId] += points;
                }

                model.TopCyclists = cyclistPoints
                    .Where(c => c.Value > 0)
                    .Select(c =>
                    {
                        var cyclist = stageResults
                            .Select(sr => sr.Cyclist)
                            .Concat(jerseys.Select(j => j.Cyclist))
                            .FirstOrDefault(x => x.Id == c.Key);

                        return new TopCyclistItem
                        {
                            Name = cyclist != null
                                ? cyclist.FirstName + " " + cyclist.LastName
                                : "Onbekende renner",
                            Points = c.Value
                        };
                    })
                    .OrderByDescending(c => c.Points)
                    .ThenBy(c => c.Name)
                    .Take(5)
                    .ToList();

                var playerSelections = await _context.PlayerSelections
                    .Include(ps => ps.Player)
                    .Include(ps => ps.Cyclist)
                    .Where(ps => ps.GameSessionId == gameId)
                    .ToListAsync();

                model.PlayerRanking = playerSelections
                    .GroupBy(ps => ps.Player)
                    .Select(group => new PlayerRankingItem
                    {
                        PlayerName = group.Key.Name,
                        Points = group.Sum(ps =>
                            cyclistPoints.ContainsKey(ps.CyclistId)
                                ? cyclistPoints[ps.CyclistId]
                                : 0)
                    })
                    .OrderByDescending(p => p.Points)
                    .ThenBy(p => p.PlayerName)
                    .ToList();

                for (int i = 0; i < model.PlayerRanking.Count; i++)
                {
                    model.PlayerRanking[i].Position = i + 1;
                }

                model.Jerseys = jerseys
                    .OrderByDescending(j => j.Stage.StageNumber)
                    .Take(4)
                    .Select(j => new JerseyItem
                    {
                        Type = j.Type,
                        CyclistName = j.Cyclist.FirstName + " " + j.Cyclist.LastName
                    })
                    .ToList();

                var latestStageWithResults = stageResults
                    .OrderByDescending(r => r.Stage.StageNumber)
                    .Select(r => r.Stage)
                    .FirstOrDefault();

                if (latestStageWithResults != null)
                {
                    model.LatestStageTitle = "Stage " + latestStageWithResults.StageNumber + " - " + latestStageWithResults.Name;

                    model.LatestStageTop3 = stageResults
                        .Where(r => r.StageId == latestStageWithResults.Id && r.Position != null)
                        .OrderBy(r => r.Position)
                        .Take(3)
                        .Select(r => r.Position + ". " + r.Cyclist.FirstName + " " + r.Cyclist.LastName)
                        .ToList();
                }
            }
            catch
            {
                ViewBag.DatabaseOnline = false;
            }

            return View(model);
        }

        private static string GetRuleTypeForJersey(string jerseyType)
        {
            return jerseyType switch
            {
                "Red" => "RodeTrui",
                "Green" => "GroeneTrui",
                "Blue" => "BlauweTrui",
                "White" => "WitteTrui",
                "Yellow" => "RodeTrui",
                "Polka" => "BlauweTrui",
                "RodeTrui" => "RodeTrui",
                "GroeneTrui" => "GroeneTrui",
                "BlauweTrui" => "BlauweTrui",
                "WitteTrui" => "WitteTrui",
                _ => jerseyType
            };
        }
    }
}
