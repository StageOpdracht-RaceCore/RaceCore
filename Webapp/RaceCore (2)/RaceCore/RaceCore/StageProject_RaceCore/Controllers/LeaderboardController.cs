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

        Belangrijk:
        - Alleen volledig afgewerkte games komen in de dropdown
        - Draft / Active / Cancelled games worden niet getoond
        - Spelers worden kolommen
        - Ritten worden rijen
        - Per rit zie je hoeveel punten elke speler heeft
        - Onderaan komt Eindafrekening
        - Onderaan komt TOTAAL
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
            // Alleen afgewerkte games ophalen voor de dropdown
            var games = await _context.GameSessions
                .Include(g => g.Race)
                .Where(g =>
                    g.Status == "Finished" ||
                    g.Status == "Done")
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

            var model = new LeaderboardViewModel
            {
                Games = games
            };

            // Als er geen afgewerkte games zijn
            if (!games.Any())
            {
                model.RaceName = "No finished game";
                model.GameStatus = "Geen";
                model.PlayerCount = 0;
                model.TotalPoints = 0;

                ViewBag.DatabaseOnline = await _context.Database.CanConnectAsync();

                return View(model);
            }

            // Geselecteerde afgewerkte game bepalen
            var selectedGame = gameId > 0
                ? games.FirstOrDefault(g => g.Id == gameId)
                : games.FirstOrDefault();

            // Als iemand via URL een gameId meegeeft die niet afgewerkt is
            if (selectedGame == null)
            {
                selectedGame = games.First();
            }

            // Basisinformatie voor de pagina
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
                Color = "#d1d5db"
            }).ToList();

            model.PlayerCount = model.Players.Count;

            // Alle ritten van de gekozen race ophalen
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
    }
}