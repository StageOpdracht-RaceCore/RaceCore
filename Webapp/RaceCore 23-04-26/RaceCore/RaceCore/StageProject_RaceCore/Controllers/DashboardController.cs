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

        public IActionResult Index()
        {
            var model = new DashboardViewModel
            {
                PlayersCount = _context.Players.Count(),
                CyclistsCount = _context.Cyclists.Count(),
                TeamsCount = _context.Teams.Count(),
                StagesCount = _context.Stages.Count(),

                TotalDraftPicks = _context.DraftTurns.Count(dt => dt.CyclistId != null),
                DraftCompleted = _context.DraftTurns.Any() && _context.DraftTurns.All(dt => dt.CyclistId != null)
            };

            model.PlayerRanking = _context.Players
                .Select(p => new PlayerRankingItem
                {
                    PlayerName = p.Name,
                    Points = _context.PlayerPoints
                        .Where(pp => pp.PlayerId == p.Id)
                        .Select(pp => (int?)pp.Points)
                        .Sum() ?? 0
                })
                .OrderByDescending(x => x.Points)
                .ThenBy(x => x.PlayerName)
                .ToList();

            for (int i = 0; i < model.PlayerRanking.Count; i++)
            {
                model.PlayerRanking[i].Position = i + 1;
            }

            model.TopCyclists = _context.Cyclists
                .Select(c => new TopCyclistItem
                {
                    Name = c.FirstName + " " + c.LastName,
                    Points = _context.PlayerPoints
                        .Where(pp => pp.CyclistId == c.Id)
                        .Select(pp => (int?)pp.Points)
                        .Sum() ?? 0
                })
                .OrderByDescending(x => x.Points)
                .ThenBy(x => x.Name)
                .Take(5)
                .ToList();

            model.Jerseys = _context.Jerseys
                .Include(j => j.Cyclist)
                .Select(j => new JerseyItem
                {
                    Type = j.Type,
                    CyclistName = j.Cyclist.FirstName + " " + j.Cyclist.LastName
                })
                .OrderBy(j => j.Type)
                .ToList();

            var latestStage = _context.Stages
                .OrderByDescending(s => s.Date)
                .ThenByDescending(s => s.StageNumber)
                .FirstOrDefault();

            if (latestStage != null)
            {
                model.LatestStageTitle = $"Stage {latestStage.StageNumber} - {latestStage.Name}";

                model.LatestStageTop3 = _context.StageResults
                    .Include(sr => sr.Cyclist)
                    .Where(sr => sr.StageId == latestStage.Id && sr.Position != null)
                    .OrderBy(sr => sr.Position)
                    .Take(3)
                    .Select(sr => $"{sr.Position}. {sr.Cyclist.FirstName} {sr.Cyclist.LastName}")
                    .ToList();
            }

            return View(model);
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}