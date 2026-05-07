using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    // ViewModel voor de Index (Algemeen klassement)
    public class ResultVM
    {
        public string StageName { get; set; } = "";
        public string CyclistName { get; set; } = "";
        public int points { get; set; }
        public string JerseyType { get; set; } = "";
        public int totalPoints { get; set; }
    }

    /* ResultController.cs
       Purpose: Provide views for game results and aggregated rankings.
       Includes helpers to map jersey types to rule types and display names.
    */
    /// <summary>
    /// Controller responsible for rendering various result views (overall leaderboard, stage results).
    /// </summary>
    public class ResultController : Controller
    {
        private readonly AppDbContext _context;

        public ResultController(AppDbContext appDbContext)
        {
            _context = appDbContext;
        }

        // Algemeen Klassement (Spelers)
        public async Task<IActionResult> Index()
        {
            var rankData = await _context.PlayerPoints
                .Include(pp => pp.Player)
                .Where(pp => pp.Player != null)
                .GroupBy(pp => pp.Player.Name)
                .Select(group => new ResultVM
                {
                    CyclistName = group.Key,
                    points = group.Sum(pp => pp.Points),
                    totalPoints = group.Sum(pp => pp.Points),
                    JerseyType = "Leader"
                })
                .OrderByDescending(r => r.totalPoints)
                .ToListAsync();

            return View(rankData);
        }

        // Resultaten per Rit
        public async Task<IActionResult> StageResults(int? raceId)
        {
            // 1. Haal alle wedstrijden op voor de dropdown
            var races = await _context.Races
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Year)
                .ToListAsync();

            if (!races.Any())
            {
                ViewBag.Races = races;
                ViewBag.SelectedRaceId = 0;
                ViewBag.StageTables = new List<object>();
                return View();
            }

            int selectedRaceId = raceId ?? races.First().Id;
            ViewBag.Races = races;
            ViewBag.SelectedRaceId = selectedRaceId;

            // 2. Haal alle ritten van de geselecteerde wedstrijd op
            var stages = await _context.Stages
                .Where(s => s.RaceId == selectedRaceId)
                .OrderBy(s => s.StageNumber)
                .ToListAsync();

            // 3. Haal de puntenregels eenmalig op voor de berekeningen
            var rules = await _context.PointsRules.ToListAsync();

            var stageTables = new List<object>();

            foreach (var stage in stages)
            {
                // Haal de top 25 uitslag op
                var results = await _context.StageResults
                    .Include(sr => sr.Cyclist)
                    .Where(sr => sr.StageId == stage.Id)
                    .OrderBy(sr => sr.Position)
                    .ToListAsync();

                // Haal de truien op (geregistreerd via Stage Input)
                var jerseys = await _context.Jerseys
                    .Include(j => j.Cyclist)
                    .Where(j => j.StageId == stage.Id)
                    .ToListAsync();

                var top25CyclistIds = results.Select(r => r.CyclistId).ToHashSet();

                // --- VERWERK TOP 25 ---
                var rows = results.Select(sr =>
                {
                    // Punten op basis van positie
                    int positionPoints = rules
                        .Where(pr => pr.Type == "Rit" && pr.FromPosition <= sr.Position && pr.ToPosition >= sr.Position)
                        .Sum(pr => pr.Points);

                    // Truien die deze renner in deze rit heeft
                    var cyclistJerseys = jerseys.Where(j => j.CyclistId == sr.CyclistId).ToList();

                    int jerseyPoints = cyclistJerseys.Sum(j =>
                        rules.Where(pr => pr.Type == GetRuleTypeForJersey(j.Type)).Sum(pr => pr.Points)
                    );

                    return new
                    {
                        Position = sr.Position.ToString(),
                        CyclistName = sr.Cyclist?.FullName ?? "Onbekend",
                        Points = positionPoints,
                        JerseyTypes = string.Join(", ", cyclistJerseys.Select(j => GetJerseyDisplayName(j.Type))),
                        JerseyPoints = jerseyPoints,
                        Total = positionPoints + jerseyPoints
                    };
                }).ToList();

                // --- VERWERK TRUIDRAGERS BUITEN TOP 25 ---
                var outsideJerseyRows = jerseys
                    .Where(j => !top25CyclistIds.Contains(j.CyclistId))
                    .GroupBy(j => j.CyclistId) // Groepeer per renner
                    .Select(group =>
                    {
                        var cyclist = group.First().Cyclist;

                        int jerseyPoints = group.Sum(j =>
                            rules.Where(pr => pr.Type == GetRuleTypeForJersey(j.Type)).Sum(pr => pr.Points)
                        );

                        return new
                        {
                            Position = ">25",
                            CyclistName = cyclist?.FullName ?? "Onbekend",
                            Points = 0,
                            JerseyTypes = string.Join(", ", group.Select(j => GetJerseyDisplayName(j.Type))),
                            JerseyPoints = jerseyPoints,
                            Total = jerseyPoints
                        };
                    }).ToList();

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

        // Matcht de database string van Stage Input met de PointsRules Type kolom
        private static string GetRuleTypeForJersey(string jerseyType)
        {
            return jerseyType switch
            {
                "Red" => "RodeTrui",
                "Green" => "GroeneTrui",
                "Blue" => "BlauweTrui",
                "White" => "WitteTrui",
                "Yellow" => "RodeTrui", // Voor het geval 'Yellow' wordt gebruikt voor de leider
                "Polka" => "BlauweTrui", // Voor het geval 'Polka' wordt gebruikt
                _ => jerseyType
            };
        }

        // Voor een nette weergave in de tabel
        private static string GetJerseyDisplayName(string type)
        {
            return type switch
            {
                "Red" or "Yellow" => "Rode trui",
                "Green" => "Groene trui",
                "Blue" or "Polka" => "Blauwe trui",
                "White" => "Witte trui",
                "RodeTrui" => "Rode trui",
                "GroeneTrui" => "Groene trui",
                "BlauweTrui" => "Blauwe trui",
                "WitteTrui" => "Witte trui",
                _ => type
            };
        }
    }
}
