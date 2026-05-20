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

            if (gameId.HasValue)
            {
                selectedGame = await _context.GameSessions
                    .Include(g => g.Race)
                    .FirstOrDefaultAsync(g => g.Id == gameId.Value);

                selectedRace = selectedGame?.Race;
            }

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

            if (selectedRace == null)
            {
                selectedGame = await _context.GameSessions
                    .Include(g => g.Race)
                    .OrderByDescending(g => g.CreatedAt)
                    .FirstOrDefaultAsync(g =>
                        g.Status == "Active" ||
                        g.Status == "Started" ||
                        g.Status == "Draft" ||
                        g.Status == "Finished");

                selectedRace = selectedGame?.Race;
            }

            selectedRace ??= races.First();

            ViewBag.SelectedRaceName = selectedRace.Name + " " + selectedRace.Year;
            ViewBag.SelectedRaceId = selectedRace.Id;
            ViewBag.SelectedGameId = selectedGame?.Id ?? 0;

            var stages = await _context.Stages
                .Where(s => s.RaceId == selectedRace.Id)
                .OrderBy(s => s.StageNumber)
                .ToListAsync();

            var rules = await _context.PointsRules.ToListAsync();

            var stageTables = new List<object>();

            foreach (var stage in stages)
            {
                var resultsQuery = _context.StageResults
                    .Include(sr => sr.Cyclist)
                    .Where(sr => sr.StageId == stage.Id);

                if (selectedGame != null)
                {
                    resultsQuery = resultsQuery.Where(sr => sr.GameSessionId == selectedGame.Id);
                }

                var results = await resultsQuery
                    .Where(sr => sr.Position.HasValue &&
                                 sr.Position.Value > 0 &&
                                 sr.Position.Value <= 25)
                    .OrderBy(sr => sr.Position)
                    .ToListAsync();

                var jerseysQuery = _context.Jerseys
                    .Include(j => j.Cyclist)
                    .Where(j => j.StageId == stage.Id);

                if (selectedGame != null)
                {
                    jerseysQuery = jerseysQuery.Where(j => j.GameSessionId == selectedGame.Id);
                }

                var jerseys = await jerseysQuery.ToListAsync();

                var top25CyclistIds = results
                    .Select(r => r.CyclistId)
                    .ToHashSet();

                // ====================================================
                // TOP 25 RESULTS
                // ====================================================
                var rows = results.Select(sr =>
                {
                    int position = sr.Position ?? 0;

                    int positionPoints = GetPositionPoints(rules, position);

                    var cyclistJerseys = jerseys
                        .Where(j => j.CyclistId == sr.CyclistId)
                        .ToList();

                    int jerseyPoints = cyclistJerseys.Sum(j =>
                        GetJerseyPoints(rules, j.Type)
                    );

                    return new
                    {
                        Position = position.ToString(),
                        CyclistName = sr.Cyclist?.FullName ?? "Unknown",
                        Points = positionPoints,

                        JerseyTypes = string.Join(", ", cyclistJerseys.Select(j =>
                            GetJerseyDisplayName(j.Type, selectedRace.Name)
                        )),

                        JerseyIcons = string.Join(" ", cyclistJerseys.Select(j =>
                            GetJerseyIconHtml(j.Type, selectedRace.Name)
                        )),

                        JerseyPoints = jerseyPoints,
                        Total = positionPoints + jerseyPoints
                    };
                }).ToList();

                // ====================================================
                // JERSEYS OUTSIDE TOP 25
                // ====================================================
                var outsideJerseyRows = jerseys
                    .Where(j => !top25CyclistIds.Contains(j.CyclistId))
                    .GroupBy(j => j.CyclistId)
                    .Select(group =>
                    {
                        var cyclist = group.First().Cyclist;

                        int jerseyPoints = group.Sum(j =>
                            GetJerseyPoints(rules, j.Type)
                        );

                        return new
                        {
                            Position = ">25",
                            CyclistName = cyclist?.FullName ?? "Unknown",
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
                    StageName = $"Stage {stage.StageNumber}: {stage.Name}",
                    Rows = rows,
                    OutsideJerseyRows = outsideJerseyRows
                });
            }

            ViewBag.StageTables = stageTables;

            return View();
        }

        // ============================================================
        // PUNTEN VOOR POSITIE BEREKENEN
        // ============================================================
        private static int GetPositionPoints(List<PointsRule> rules, int position)
        {
            int pointsFromRules = rules
                .Where(pr =>
                    (pr.Type == "Rit" || pr.Type == "Stage" || pr.Type == "Etappe") &&
                    pr.FromPosition.HasValue &&
                    pr.ToPosition.HasValue &&
                    pr.FromPosition.Value <= position &&
                    pr.ToPosition.Value >= position)
                .Sum(pr => pr.Points);

            if (pointsFromRules > 0)
            {
                return pointsFromRules;
            }

            return position switch
            {
                1 => 100,
                2 => 80,
                3 => 65,
                4 => 55,
                5 => 45,
                6 => 35,
                7 => 30,
                8 => 25,
                9 => 20,
                10 => 17,
                11 => 15,
                12 => 13,
                13 => 11,
                14 => 10,
                15 => 9,
                16 => 8,
                17 => 7,
                18 => 6,
                19 => 5,
                20 => 4,
                21 => 3,
                22 => 2,
                23 => 1,
                24 => 1,
                25 => 1,
                _ => 0
            };
        }

        // ============================================================
        // TRUIPUNTEN BEREKENEN
        // ============================================================
        private static int GetJerseyPoints(List<PointsRule> rules, string jerseyType)
        {
            string ruleType = GetRuleTypeForJersey(jerseyType);

            int pointsFromRules = rules
                .Where(pr => pr.Type == ruleType)
                .Sum(pr => pr.Points);

            return pointsFromRules > 0 ? pointsFromRules : 10;
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
                    "Red" or "Yellow" or "RodeTrui" => "Pink Jersey",
                    "Green" or "GroeneTrui" => "Purple Jersey",
                    "Blue" or "Polka" or "BlauweTrui" => "Blue Jersey",
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
                    "Blue" or "Polka" or "BlauweTrui" => "Polka Dot Jersey",
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
                    "Blue" or "Polka" or "BlauweTrui" => "Polka Dot Jersey",
                    "White" or "WitteTrui" => "White Jersey",
                    _ => type
                };
            }

            return type switch
            {
                "Red" or "Yellow" or "RodeTrui" => "Red Jersey",
                "Green" or "GroeneTrui" => "Green Jersey",
                "Blue" or "Polka" or "BlauweTrui" => "Blue Jersey",
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
                "Polka Dot Jersey" => "<span class=\"result-jersey-dot result-jersey-polka\" title=\"Polka Dot Jersey\"></span>",
                "White Jersey" => "<span class=\"result-jersey-dot result-jersey-white\" title=\"White Jersey\"></span>",
                "Pink Jersey" => "<span class=\"result-jersey-dot result-jersey-pink\" title=\"Pink Jersey\"></span>",
                "Purple Jersey" => "<span class=\"result-jersey-dot result-jersey-purple\" title=\"Purple Jersey\"></span>",
                "Blue Jersey" => "<span class=\"result-jersey-dot result-jersey-blue\" title=\"Blue Jersey\"></span>",
                "Red Jersey" => "<span class=\"result-jersey-dot result-jersey-red\" title=\"Red Jersey\"></span>",
                _ => "<span class=\"result-jersey-dot result-jersey-default\" title=\"Jersey\"></span>"
            };
        }
    }
}