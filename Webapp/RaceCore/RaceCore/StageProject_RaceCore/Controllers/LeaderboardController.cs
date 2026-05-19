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
        - Alle games behalve Cancelled komen in de dropdown
        - Draft / Active / Finished / Done games kunnen getoond worden
        - Zo kan je ook vorige games zien die nog Active staan
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
            try
            {
                // Alle games ophalen voor de dropdown, behalve geannuleerde games
                var games = await _context.GameSessions
                    .Include(g => g.Race)
                    .Where(g => g.Status != "Cancelled")
                    .OrderByDescending(g => g.CreatedAt)
                    .ToListAsync();

                var model = new LeaderboardViewModel
                {
                    Games = games
                };

                // Als er geen games zijn
                if (!games.Any())
                {
                    model.RaceName = "No game found";
                    model.GameStatus = "None";
                    model.PlayerCount = 0;
                    model.TotalPoints = 0;

                    ViewBag.DatabaseOnline = await _context.Database.CanConnectAsync();

                    return View(model);
                }

                // Geselecteerde game bepalen
                var selectedGame = gameId > 0
                    ? games.FirstOrDefault(g => g.Id == gameId)
                    : games.FirstOrDefault();

                // Als iemand via URL een gameId meegeeft die niet bestaat of Cancelled is
                if (selectedGame == null)
                {
                    selectedGame = games.First();
                }

                // Basisinformatie voor de pagina
                model.SelectedGameId = selectedGame.Id;
                model.RaceName = selectedGame.Race?.Name ?? "Unknown race";
                model.GameStatus = selectedGame.Status;
                model.CreatedAt = selectedGame.CreatedAt;

                // Spelerselecties ophalen van deze game
                var selections = await _context.PlayerSelections
                    .Where(s => s.GameSessionId == selectedGame.Id)
                    .Include(s => s.Player)
                    .ToListAsync();

                // Spelers ophalen die effectief meedoen in deze game
                var players = selections
                    .Where(s => s.Player != null)
                    .Select(s => s.Player!)
                    .GroupBy(p => p.Id)
                    .Select(g => g.First())
                    .OrderBy(p => p.Name)
                    .ToList();

                // Als er nog geen selections zijn, spelers ophalen via PlayerPoints
                if (!players.Any())
                {
                    players = await _context.PlayerPoints
                        .Where(pp => pp.GameSessionId == selectedGame.Id)
                        .Include(pp => pp.Player)
                        .Where(pp => pp.Player != null)
                        .Select(pp => pp.Player!)
                        .Distinct()
                        .OrderBy(p => p.Name)
                        .ToListAsync();
                }

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

                // Alle punten ophalen van deze game
                var playerPoints = await _context.PlayerPoints
                    .Where(pp => pp.GameSessionId == selectedGame.Id)
                    .ToListAsync();

                // Voor elke rit een rij maken
                foreach (var stage in stages)
                {
                    var stageRow = new LeaderboardStageRowViewModel
                    {
                        StageId = stage.Id,
                        StageNumber = stage.StageNumber,
                        StageName = string.IsNullOrWhiteSpace(stage.Name)
                            ? $"Stage {stage.StageNumber}"
                            : stage.Name
                    };

                    // Voor elke speler punten ophalen voor deze rit
                    foreach (var player in players)
                    {
                        int stagePointsForPlayer = playerPoints
                            .Where(pp =>
                                pp.PlayerId == player.Id &&
                                pp.StageId == stage.Id)
                            .Sum(pp => pp.Points);

                        stageRow.PointsPerPlayer[player.Id] = stagePointsForPlayer;
                    }

                    model.StageRows.Add(stageRow);
                }

                // Eindafrekening ophalen per speler
                foreach (var player in players)
                {
                    int finalSettlementPoints = playerPoints
                        .Where(pp =>
                            pp.PlayerId == player.Id &&
                            pp.StageId == null)
                        .Sum(pp => pp.Points);

                    model.FinalSettlementPoints[player.Id] = finalSettlementPoints;
                }

                // Totaal per speler berekenen
                foreach (var player in players)
                {
                    int totalPoints = playerPoints
                        .Where(pp => pp.PlayerId == player.Id)
                        .Sum(pp => pp.Points);

                    model.TotalPointsPerPlayer[player.Id] = totalPoints;
                }

                // Totaal punten van heel de game
                model.TotalPoints = model.TotalPointsPerPlayer.Sum(p => p.Value);

                ViewBag.DatabaseOnline = await _context.Database.CanConnectAsync();

                return View(model);
            }
            catch (Exception ex)
            {
                ViewBag.DatabaseOnline = false;
                TempData["Error"] = "Leaderboard could not be loaded: " + ex.Message;

                return View(new LeaderboardViewModel
                {
                    RaceName = "Leaderboard error",
                    GameStatus = "Error",
                    PlayerCount = 0,
                    TotalPoints = 0,
                    Games = new List<GameSession>()
                });
            }
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