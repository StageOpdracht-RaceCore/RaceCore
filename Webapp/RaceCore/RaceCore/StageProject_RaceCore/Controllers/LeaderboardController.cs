using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    /*
        ============================================================
        Controller: LeaderboardController
        ============================================================

        Doel:
        Deze controller maakt de leaderboard pagina.

        Nieuwe werking:
        - spelers worden kolommen
        - ritten worden rijen
        - per rit zie je hoeveel punten elke speler heeft
        - onderaan komt Eindafrekening
        - onderaan komt TOTAAL
    */

    public class LeaderboardController : Controller
    {
        private readonly AppDbContext _context;

        public LeaderboardController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // INDEX - LEADERBOARD TONEN
        // ============================================================

        public async Task<IActionResult> Index(int gameId = 0)
        {
            // Alle games ophalen voor de dropdown
            var games = await _context.GameSessions
                .Include(g => g.Race)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

            var model = new LeaderboardViewModel
            {
                Games = games
            };

            // Als er nog geen games zijn, toon lege pagina
            if (!games.Any())
            {
                return View(model);
            }

            // Geselecteerde game bepalen
            var selectedGame = gameId > 0
                ? games.FirstOrDefault(g => g.Id == gameId)
                : games.FirstOrDefault();

            if (selectedGame == null)
            {
                selectedGame = games.First();
            }

            // Basisinfo invullen
            model.SelectedGameId = selectedGame.Id;
            model.RaceName = selectedGame.Race?.Name ?? "Onbekende race";
            model.GameStatus = selectedGame.Status;
            model.CreatedAt = selectedGame.CreatedAt;

            // Spelerselecties ophalen van deze game
            var selections = await _context.PlayerSelections
                .Where(s => s.GameSessionId == selectedGame.Id)
                .Include(s => s.Player)
                .Include(s => s.Cyclist)
                .ToListAsync();

            // Spelers ophalen die effectief meedoen in deze game
            var players = selections
                .Where(s => s.Player != null)
                .Select(s => s.Player!)
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .OrderBy(p => p.Name)
                .ToList();

            // Spelers als kolommen klaarzetten
            model.Players = players.Select(p => new LeaderboardPlayerColumnViewModel
            {
                PlayerId = p.Id,
                PlayerName = p.Name,
                Color = GetPlayerColor(p.Name)
            }).ToList();

            model.PlayerCount = model.Players.Count;

            // Alle ritten van de race ophalen
            var stages = await _context.Stages
                .Where(s => s.RaceId == selectedGame.RaceId)
                .OrderBy(s => s.StageNumber)
                .ToListAsync();

            var stageIds = stages.Select(s => s.Id).ToList();

            // Alle ritresultaten ophalen
            var stageResults = await _context.StageResults
                .Where(sr => stageIds.Contains(sr.StageId))
                .ToListAsync();

            // Alle truien ophalen
            var jerseys = await _context.Jerseys
                .Where(j => stageIds.Contains(j.StageId))
                .ToListAsync();

            // Puntenregels ophalen
            var rules = await _context.PointsRules
                .ToListAsync();

            // Per speler de gekozen renners bijhouden
            var ridersByPlayer = selections
                .Where(s => s.Cyclist != null)
                .GroupBy(s => s.PlayerId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(s => s.Cyclist!.Id).ToList()
                );

            // Voor elke rit een rij maken
            foreach (var stage in stages)
            {
                var stageRow = new LeaderboardStageRowViewModel
                {
                    StageId = stage.Id,
                    StageNumber = stage.StageNumber,
                    StageName = string.IsNullOrWhiteSpace(stage.Name)
                        ? $"Rit {stage.StageNumber}"
                        : stage.Name
                };

                // Voor elke speler punten berekenen voor deze rit
                foreach (var player in players)
                {
                    int stagePointsForPlayer = 0;

                    if (ridersByPlayer.TryGetValue(player.Id, out var cyclistIds))
                    {
                        foreach (var cyclistId in cyclistIds)
                        {
                            // Punten uit ritpositie
                            var result = stageResults.FirstOrDefault(r =>
                                r.StageId == stage.Id &&
                                r.CyclistId == cyclistId);

                            if (result != null)
                            {
                                stagePointsForPlayer += rules
                                    .Where(r =>
                                        r.Type == "Rit" &&
                                        r.FromPosition <= result.Position &&
                                        r.ToPosition >= result.Position)
                                    .Sum(r => r.Points);
                            }

                            // Punten uit truien
                            var cyclistJerseys = jerseys.Where(j =>
                                j.StageId == stage.Id &&
                                j.CyclistId == cyclistId);

                            foreach (var jersey in cyclistJerseys)
                            {
                                string ruleType = ConvertJerseyTypeToRuleType(jersey.Type);

                                stagePointsForPlayer += rules
                                    .Where(r => r.Type == ruleType)
                                    .Sum(r => r.Points);
                            }
                        }
                    }

                    stageRow.PointsPerPlayer[player.Id] = stagePointsForPlayer;
                }

                model.StageRows.Add(stageRow);
            }

            // Eindafrekening voorlopig op 0 per speler
            // Later kan je hier aparte eindpunten toevoegen als je daarvoor een tabel maakt
            foreach (var player in players)
            {
                model.FinalSettlementPoints[player.Id] = 0;
            }

            // Totaal per speler berekenen
            foreach (var player in players)
            {
                int totalFromStages = model.StageRows
                    .Sum(r => r.PointsPerPlayer.ContainsKey(player.Id)
                        ? r.PointsPerPlayer[player.Id]
                        : 0);

                int finalPoints = model.FinalSettlementPoints.ContainsKey(player.Id)
                    ? model.FinalSettlementPoints[player.Id]
                    : 0;

                model.TotalPointsPerPlayer[player.Id] = totalFromStages + finalPoints;
            }

            // Totaal punten van heel de game
            model.TotalPoints = model.TotalPointsPerPlayer.Sum(p => p.Value);

            ViewBag.DatabaseOnline = await _context.Database.CanConnectAsync();

            return View(model);
        }

        // ============================================================
        // HELPER - TRUI TYPE OMZETTEN NAAR POINTSRULE TYPE
        // ============================================================

        private string ConvertJerseyTypeToRuleType(string type)
        {
            return type switch
            {
                "Red" => "RodeTrui",
                "Green" => "GroeneTrui",
                "Blue" => "BlauweTrui",
                "White" => "WitteTrui",
                _ => type
            };
        }

        // ============================================================
        // HELPER - SPELER KLEUR
        // ============================================================

        private string GetPlayerColor(string name)
        {
            var colors = new List<string>
            {
                "#3b82f6",
                "#f59e0b",
                "#22c55e",
                "#db2777",
                "#ca8a04",
                "#9333ea",
                "#0891b2",
                "#dc2626"
            };

            int hash = name?.GetHashCode() ?? 0;
            return colors[Math.Abs(hash) % colors.Count];
        }
    }
}