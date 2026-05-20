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

        // ============================================================
        // ALGEMEEN KLASSEMENT VAN SPELERS
        // ============================================================
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

        // ============================================================
        // RESULTATEN PER RIT TONEN
        // ============================================================
        public async Task<IActionResult> StageResults(int? raceId, int? gameId)
        {
            // Alle races ophalen
            var races = await _context.Races
                .OrderByDescending(r => r.Year)
                .ThenByDescending(r => r.StartDate)
                .ToListAsync();

            ViewBag.Races = races;

            if (!races.Any())
            {
                ViewBag.StageTables = new List<object>();
                return View();
            }

            GameSession? selectedGame = null;
            Race? selectedRace = null;

            // Eerst gameId gebruiken, want dat is het meest correct
            if (gameId.HasValue)
            {
                selectedGame = await _context.GameSessions
                    .Include(g => g.Race)
                    .FirstOrDefaultAsync(g => g.Id == gameId.Value);

                selectedRace = selectedGame?.Race;
            }

            // Daarna zoeken via raceId
            if (selectedRace == null && raceId.HasValue)
            {
                selectedRace = races.FirstOrDefault(r => r.Id == raceId.Value);

                if (selectedRace != null)
                {
                    selectedGame = await _context.GameSessions
                        .Include(g => g.Race)
                        .Where(g => g.RaceId == selectedRace.Id)
                        .OrderByDescending(g => g.CreatedAt)
                        .FirstOrDefaultAsync();
                }
            }

            // Fallback naar laatste actieve game
            if (selectedRace == null)
            {
                selectedGame = await _context.GameSessions
                    .Include(g => g.Race)
                    .OrderByDescending(g => g.CreatedAt)
                    .FirstOrDefaultAsync(g =>
                        g.Status == "Active" ||
                        g.Status == "Started" ||
                        g.Status == "Draft");

                selectedRace = selectedGame?.Race;
            }

            // Laatste fallback
            if (selectedRace == null)
            {
                selectedRace = races.First();
            }

            ViewBag.SelectedRaceName = selectedRace.Name + " " + selectedRace.Year;
            ViewBag.SelectedRaceId = selectedRace.Id;
            ViewBag.SelectedGameId = selectedGame?.Id ?? 0;

            // Ritten ophalen van deze race
            var stages = await _context.Stages
                .Where(s => s.RaceId == selectedRace.Id)
                .OrderBy(s => s.StageNumber)
                .ToListAsync();

            // Puntenregels ophalen
            var rules = await _context.PointsRules.ToListAsync();

            var stageTables = new List<object>();

            foreach (var stage in stages)
            {
                // Resultaten ophalen
                var resultsQuery = _context.StageResults
                    .Include(sr => sr.Cyclist)
                    .Where(sr => sr.StageId == stage.Id);

                // Belangrijk: filteren op game zodat je geen oude game data ziet
                if (selectedGame != null)
                {
                    resultsQuery = resultsQuery.Where(sr => sr.GameSessionId == selectedGame.Id);
                }

                var results = await resultsQuery
                    .OrderBy(sr => sr.Position)
                    .ToListAsync();

                // Truien ophalen
                var jerseysQuery = _context.Jerseys
                    .Include(j => j.Cyclist)
                    .Where(j => j.StageId == stage.Id);

                // Belangrijk: ook truien filteren op game
                if (selectedGame != null)
                {
                    jerseysQuery = jerseysQuery.Where(j => j.GameSessionId == selectedGame.Id);
                }

                var jerseys = await jerseysQuery.ToListAsync();

                var top25CyclistIds = results
                    .Select(r => r.CyclistId)
                    .ToHashSet();

                // ====================================================
                // TOP 25 RESULTATEN
                // ====================================================
                var rows = results.Select(sr =>
                {
                    int position = sr.Position ?? 0;

                    int positionPoints = rules
                        .Where(pr =>
                            pr.Type == "Rit" &&
                            pr.FromPosition <= position &&
                            pr.ToPosition >= position)
                        .Sum(pr => pr.Points);

                    var cyclistJerseys = jerseys
                        .Where(j => j.CyclistId == sr.CyclistId)
                        .ToList();

                    int jerseyPoints = cyclistJerseys.Sum(j =>
                        rules
                            .Where(pr => pr.Type == GetRuleTypeForJersey(j.Type))
                            .Sum(pr => pr.Points)
                    );

                    return new
                    {
                        Position = position.ToString(),
                        CyclistName = sr.Cyclist?.FullName ?? "Onbekend",
                        Points = positionPoints,

                        // Dit is de tekstnaam, handig voor title/controle
                        JerseyTypes = string.Join(", ", cyclistJerseys.Select(j =>
                            GetJerseyDisplayName(j.Type, selectedRace.Name)
                        )),

                        // Dit is de echte visuele cirkel die je wil tonen
                        JerseyIcons = string.Join(" ", cyclistJerseys.Select(j =>
                            GetJerseyIconHtml(j.Type, selectedRace.Name)
                        )),

                        JerseyPoints = jerseyPoints,
                        Total = positionPoints + jerseyPoints
                    };
                }).ToList();

                // ====================================================
                // TRUIEN BUITEN TOP 25
                // ====================================================
                var outsideJerseyRows = jerseys
                    .Where(j => !top25CyclistIds.Contains(j.CyclistId))
                    .GroupBy(j => j.CyclistId)
                    .Select(group =>
                    {
                        var cyclist = group.First().Cyclist;

                        int jerseyPoints = group.Sum(j =>
                            rules
                                .Where(pr => pr.Type == GetRuleTypeForJersey(j.Type))
                                .Sum(pr => pr.Points)
                        );

                        return new
                        {
                            Position = ">25",
                            CyclistName = cyclist?.FullName ?? "Onbekend",
                            Points = 0,

                            JerseyTypes = string.Join(", ", group.Select(j =>
                                GetJerseyDisplayName(j.Type, selectedRace.Name)
                            )),

                            JerseyIcons = string.Join(" ", group.Select(j =>
                                GetJerseyIconHtml(j.Type, selectedRace.Name)
                            )),

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

        // ============================================================
        // TRUI TYPE OMZETTEN NAAR POINTSRULE TYPE
        // ============================================================
        private static string GetRuleTypeForJersey(string jerseyType)
        {
            return jerseyType switch
            {
                "Red" => "RodeTrui",
                "Yellow" => "RodeTrui",

                "Green" => "GroeneTrui",

                "Blue" => "BlauweTrui",
                "Polka" => "BlauweTrui",

                "White" => "WitteTrui",

                _ => jerseyType
            };
        }

        // ============================================================
        // TRUI NAAM CORRECT TONEN PER RACE
        // ============================================================
        private static string GetJerseyDisplayName(string type, string raceName)
        {
            bool isGiro = raceName.Contains("Giro", StringComparison.OrdinalIgnoreCase);
            bool isTour = raceName.Contains("Tour", StringComparison.OrdinalIgnoreCase);
            bool isVuelta = raceName.Contains("Vuelta", StringComparison.OrdinalIgnoreCase);

            if (isGiro)
            {
                return type switch
                {
                    "Red" or "Yellow" or "RodeTrui" => "Roze trui",
                    "Green" or "GroeneTrui" => "Paarse trui",
                    "Blue" or "Polka" or "BlauweTrui" => "Blauwe trui",
                    "White" or "WitteTrui" => "White Jersey",
                    _ => type
                };
            }

            if (isTour)
            {
                return type switch
                {
                    "Red" or "Yellow" or "RodeTrui" => "Yellow Jersey",
                    "Green" or "GroeneTrui" => "Green Jersey",
                    "Blue" or "Polka" or "BlauweTrui" => "Bolletjestrui",
                    "White" or "WitteTrui" => "White Jersey",
                    _ => type
                };
            }

            if (isVuelta)
            {
                return type switch
                {
                    "Red" or "Yellow" or "RodeTrui" => "Red Jersey",
                    "Green" or "GroeneTrui" => "Green Jersey",
                    "Blue" or "Polka" or "BlauweTrui" => "Bolletjestrui",
                    "White" or "WitteTrui" => "White Jersey",
                    _ => type
                };
            }

            return type switch
            {
                "Red" or "Yellow" or "RodeTrui" => "Red Jersey",
                "Green" or "GroeneTrui" => "Green Jersey",
                "Blue" or "Polka" or "BlauweTrui" => "Blauwe trui",
                "White" or "WitteTrui" => "White Jersey",
                _ => type
            };
        }

        // ============================================================
        // HTML VOOR DE JUISTE TRUI-CIRKEL
        // ============================================================
        private static string GetJerseyIconHtml(string type, string raceName)
        {
            string displayName = GetJerseyDisplayName(type, raceName);

            return displayName switch
            {
                "Yellow Jersey" => "<span class=\"result-jersey-dot result-jersey-yellow\" title=\"Yellow Jersey\"></span>",
                "Green Jersey" => "<span class=\"result-jersey-dot result-jersey-green\" title=\"Green Jersey\"></span>",
                "Bolletjestrui" => "<span class=\"result-jersey-dot result-jersey-polka\" title=\"Bolletjestrui\"></span>",
                "White Jersey" => "<span class=\"result-jersey-dot result-jersey-white\" title=\"White Jersey\"></span>",
                "Roze trui" => "<span class=\"result-jersey-dot result-jersey-pink\" title=\"Roze trui\"></span>",
                "Paarse trui" => "<span class=\"result-jersey-dot result-jersey-purple\" title=\"Paarse trui\"></span>",
                "Blauwe trui" => "<span class=\"result-jersey-dot result-jersey-blue\" title=\"Blauwe trui\"></span>",
                "Red Jersey" => "<span class=\"result-jersey-dot result-jersey-red\" title=\"Red Jersey\"></span>",
                _ => "<span class=\"result-jersey-dot result-jersey-default\" title=\"Trui\"></span>"
            };
        }
    }
}