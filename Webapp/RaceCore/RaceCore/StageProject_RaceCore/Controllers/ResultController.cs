using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class ResultVM
    {
        public string StageName { get; set; } = "";
        public string CyclistName { get; set; } = "";
        public int points { get; set; }
        public string JerseyType { get; set; } = "";
        public int totalPoints { get; set; }
    }

    public class ResultController : Controller
    {
        private readonly AppDbContext _context;

        public ResultController(AppDbContext appDbContext)
        {
            _context = appDbContext;
        }

        public async Task<IActionResult> Index()
        {
            var rankData = await _context.PlayerPoints
                .Include(pp => pp.Player)
                .GroupBy(pp => pp.Player.Name)
                .Select(group => new ResultVM
                {
                    CyclistName = group.Key,
                    totalPoints = group.Sum(pp => pp.Points),
                    JerseyType = "Leader"
                })
                .OrderByDescending(r => r.totalPoints)
                .ToListAsync();

            return View(rankData);
        }

        public async Task<IActionResult> StageResults(int? raceId)
        {
            var races = await _context.Races
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Year)
                .ToListAsync();

            if (!races.Any())
            {
                ViewBag.Races = races;
                ViewBag.StageTables = new List<object>();
                return View();
            }

            int selectedRaceId = raceId ?? races.First().Id;

            ViewBag.Races = races;
            ViewBag.SelectedRaceId = selectedRaceId;

            var stages = await _context.Stages
                .Where(s => s.RaceId == selectedRaceId)
                .OrderBy(s => s.StageNumber)
                .ToListAsync();

            var rules = await _context.PointsRules.ToListAsync();

            var stageTables = new List<object>();

            foreach (var stage in stages)
            {
                var results = await _context.StageResults
                    .Include(sr => sr.Cyclist)
                    .Where(sr => sr.StageId == stage.Id)
                    .OrderBy(sr => sr.Position)
                    .ToListAsync();

                var jerseys = await _context.Jerseys
                    .Include(j => j.Cyclist)
                    .Where(j => j.StageId == stage.Id)
                    .ToListAsync();

                var top25CyclistIds = results
                    .Select(r => r.CyclistId)
                    .ToHashSet();

                var rows = results.Select(sr =>
                {
                    int normalPoints = rules
                        .Where(pr =>
                            pr.Type == "Stage" &&
                            sr.Position != null &&
                            pr.FromPosition <= sr.Position &&
                            pr.ToPosition >= sr.Position)
                        .Sum(pr => pr.Points);

                    var cyclistJerseys = jerseys
                        .Where(j => j.CyclistId == sr.CyclistId)
                        .ToList();

                    int jerseyPoints = cyclistJerseys
                        .Sum(j => rules
                            .Where(pr => pr.Type == j.Type)
                            .Sum(pr => pr.Points));

                    string jerseyTypes = string.Join(", ", cyclistJerseys.Select(j => GetJerseyName(j.Type)));

                    return new
                    {
                        Position = sr.Position,
                        CyclistName = sr.Cyclist.FullName,
                        Points = normalPoints,
                        JerseyTypes = jerseyTypes,
                        JerseyPoints = jerseyPoints,
                        Total = normalPoints + jerseyPoints
                    };
                }).ToList();

                var outsideJerseyRows = jerseys
                    .Where(j => !top25CyclistIds.Contains(j.CyclistId))
                    .Select(j =>
                    {
                        int jerseyPoints = rules
                            .Where(pr => pr.Type == j.Type)
                            .Sum(pr => pr.Points);

                        return new
                        {
                            Position = "-",
                            CyclistName = j.Cyclist.FullName,
                            Points = 0,
                            JerseyTypes = GetJerseyName(j.Type),
                            JerseyPoints = jerseyPoints,
                            Total = jerseyPoints
                        };
                    })
                    .ToList();

                stageTables.Add(new
                {
                    StageName = $"Rit {stage.StageNumber}: {stage.Name}",
                    Rows = rows,
                    OutsideJerseyRows = outsideJerseyRows
                });
            }

            ViewBag.StageTables = stageTables;

            return View();
        }

        private static string GetJerseyName(string type)
        {
            return type switch
            {
                "Yellow" => "Gele trui",
                "Green" => "Groene trui",
                "Polka" => "Bolletjestrui",
                "White" => "Witte trui",
                _ => type
            };
        }
    }
}