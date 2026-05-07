using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    // ViewModel voor de Index (Algemeen klassement van spelers)
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

        // Algemeen Klassement (Spelers ranking)
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

        // Resultaten per Rit (Met automatische race-selectie)
        public async Task<IActionResult> StageResults(int? raceId)
        {
            // 1. Haal alle races op voor de logica (gesorteerd op recentheid)
            var races = await _context.Races
                .OrderByDescending(r => r.Year)
                .ThenByDescending(r => r.StartDate)
                .ToListAsync();

            if (!races.Any())
            {
                ViewBag.StageTables = new List<object>();
                return View();
            }

            Race selectedRace = null;

            // --- AUTOMATISCHE SELECTIE LOGICA ---
            if (raceId.HasValue)
            {
                // Gebruiker heeft expliciet een ID meegegeven
                selectedRace = races.FirstOrDefault(r => r.Id == raceId.Value);
            }
            else
            {
                // Optie A: Zoek de race van de meest recente actieve GameSession
                var activeGame = await _context.GameSessions
                    .Include(g => g.Race)
                    .OrderByDescending(g => g.CreatedAt)
                    .FirstOrDefaultAsync(g => g.Status == "Active" || g.Status == "Started");

                if (activeGame != null)
                {
                    selectedRace = activeGame.Race;
                }

                // Optie B (Fallback): Als er geen actieve game is, pak de race die vandaag bezig is
                if (selectedRace == null)
                {
                    selectedRace = races.FirstOrDefault(r => r.StartDate <= DateTime.Now && r.EndDate >= DateTime.Now);
                }

                // Optie C (Laatste redmiddel): Pak de allernieuwste race uit de lijst
                if (selectedRace == null)
                {
                    selectedRace = races.First();
                }
            }

            // Geef de info door naar de view voor de header en eventuele links
            ViewBag.SelectedRaceName = selectedRace.Name + " " + selectedRace.Year;
            ViewBag.SelectedRaceId = selectedRace.Id;

            // 2. Haal alle ritten van de geselecteerde wedstrijd op
            var stages = await _context.Stages
                .Where(s => s.RaceId == selectedRace.Id)
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

                // Haal de truien op
                var jerseys = await _context.Jerseys
                    .Include(j => j.Cyclist)
                    .Where(j => j.StageId == stage.Id)
                    .ToListAsync();

                var top25CyclistIds = results.Select(r => r.CyclistId).ToHashSet();

                // --- VERWERK TOP 25 ---
                var rows = results.Select(sr => {
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
                    .GroupBy(j => j.CyclistId)
                    .Select(group => {
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

        // Helper: Matcht de database string van Stage Input met de PointsRules Type kolom
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
                _ => jerseyType
            };
        }

        // Helper: Voor een nette weergave in de tabel
        private static string GetJerseyDisplayName(string type)
        {
            return type switch
            {
                "Red" or "Yellow" or "RodeTrui" => "Rode trui",
                "Green" or "GroeneTrui" => "Groene trui",
                "Blue" or "Polka" or "BlauweTrui" => "Blauwe trui",
                "White" or "WitteTrui" => "Witte trui",
                _ => type
            };
        }
    }
}