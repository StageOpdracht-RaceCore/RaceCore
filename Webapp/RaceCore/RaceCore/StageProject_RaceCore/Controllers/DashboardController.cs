using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    /* =========================================================
       DashboardController.cs

       Deze controller beheert de dashboard pagina van RaceCore.
       Hier worden alle gegevens opgehaald zoals:
       - actieve game
       - spelers ranking
       - top wielrenners
       - truien
       - statistieken
       ========================================================= */

    /// <summary>
    /// Controller van de dashboard pagina.
    /// </summary>
    public class DashboardController : Controller
    {
        // Database context om gegevens uit de database te halen
        private readonly AppDbContext _context;

        // Constructor van de controller
        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        // Dashboard pagina laden
        public async Task<IActionResult> Index(int gameId)
        {
            // Nieuw dashboard model aanmaken
            var model = new DashboardViewModel();

            try
            {
                /* =========================================================
                   CONTROLEREN OF ER EEN GAME GEKOZEN IS
                   ========================================================= */

                // Indien er geen gameId is meegegeven
                if (gameId <= 0)
                {
                    // Laatste aangemaakte game ophalen
                    var latestGame = await _context.GameSessions
                        .OrderByDescending(g => g.CreatedAt)
                        .FirstOrDefaultAsync();

                    // Indien er nog geen game bestaat
                    if (latestGame == null)
                    {
                        TempData["Error"] = "Start a game first.";

                        // Doorsturen naar nieuwe game pagina
                        return RedirectToAction("New", "Game");
                    }

                    // Laatste game gebruiken
                    gameId = latestGame.Id;
                }

                /* =========================================================
                   GAME GEGEVENS OPHALEN
                   ========================================================= */

                // Game ophalen samen met race gegevens
                var game = await _context.GameSessions
                    .Include(g => g.Race)
                    .FirstOrDefaultAsync(g => g.Id == gameId);

                // Indien game niet bestaat
                if (game == null)
                {
                    TempData["Error"] = "Game not found.";

                    return RedirectToAction("New", "Game");
                }

                /* =========================================================
                   GEGEVENS NAAR DE VIEW STUREN
                   ========================================================= */

                ViewBag.GameId = game.Id;
                ViewBag.RaceId = game.RaceId;
                ViewBag.GameStatus = game.Status;

                // Naam van de race tonen
                ViewBag.RaceName = game.Race.Name + " " + game.Race.Year;

                // Huidige etappe nummer
                ViewBag.CurrentStage = game.CurrentStageNumber;

                // Database status
                ViewBag.DatabaseOnline = true;

                /* =========================================================
                   DASHBOARD STATISTIEKEN
                   ========================================================= */

                // Aantal unieke spelers tellen
                model.PlayersCount = await _context.DraftTurns
                    .Where(d => d.GameSessionId == gameId)
                    .Select(d => d.PlayerId)
                    .Distinct()
                    .CountAsync();

                // Totaal aantal draft picks tellen
                model.TotalDraftPicks = await _context.DraftTurns
                    .Where(d => d.GameSessionId == gameId && d.CyclistId != null)
                    .CountAsync();

                // Wielrenners aantal gelijk zetten aan picks
                model.CyclistsCount = model.TotalDraftPicks;

                // Totaal aantal teams ophalen
                model.TeamsCount = await _context.Teams.CountAsync();

                // Aantal etappes van de race tellen
                model.StagesCount = await _context.Stages
                    .Where(s => s.RaceId == game.RaceId)
                    .CountAsync();

                // Controleren of draft gedaan is
                model.DraftCompleted = game.Status != "Draft";

                /* =========================================================
                   PUNTENREGELS OPHALEN
                   ========================================================= */

                // Alle puntenregels ophalen
                var rules = await _context.PointsRules.ToListAsync();

                /* =========================================================
                   ETAPPE RESULTATEN OPHALEN
                   ========================================================= */

                // Alle stage resultaten ophalen
                var stageResults = await _context.StageResults
                    .Include(sr => sr.Stage)
                    .Include(sr => sr.Cyclist)
                    .Where(sr => sr.Stage.RaceId == game.RaceId)
                    .ToListAsync();

                /* =========================================================
                   TRUIEN OPHALEN
                   ========================================================= */

                // Alle truien ophalen
                var jerseys = await _context.Jerseys
                    .Include(j => j.Stage)
                    .Include(j => j.Cyclist)
                    .Where(j => j.Stage.RaceId == game.RaceId)
                    .ToListAsync();

                /* =========================================================
                   PUNTEN PER WIELRENNER BEREKENEN
                   ========================================================= */

                // Dictionary voor punten per renner
                var cyclistPoints = new Dictionary<int, int>();

                // Door alle stage resultaten gaan
                foreach (var result in stageResults)
                {
                    // Indien positie leeg is overslaan
                    if (result.Position == null)
                    {
                        continue;
                    }

                    // Punten berekenen op basis van positie
                    int points = rules
                        .Where(r =>
                            r.Type == "Stage" &&
                            r.FromPosition <= result.Position &&
                            r.ToPosition >= result.Position)
                        .Sum(r => r.Points);

                    // Indien renner nog niet bestaat toevoegen
                    if (!cyclistPoints.ContainsKey(result.CyclistId))
                    {
                        cyclistPoints[result.CyclistId] = 0;
                    }

                    // Punten toevoegen
                    cyclistPoints[result.CyclistId] += points;
                }

                /* =========================================================
                   EXTRA PUNTEN VOOR TRUIEN
                   ========================================================= */

                foreach (var jersey in jerseys)
                {
                    // Punten ophalen van trui type
                    int points = rules
                        .Where(r => r.Type == GetRuleTypeForJersey(jersey.Type))
                        .Sum(r => r.Points);

                    // Indien renner nog niet bestaat toevoegen
                    if (!cyclistPoints.ContainsKey(jersey.CyclistId))
                    {
                        cyclistPoints[jersey.CyclistId] = 0;
                    }

                    // Punten toevoegen
                    cyclistPoints[jersey.CyclistId] += points;
                }

                /* =========================================================
                   TOP WIELRENNERS MAKEN
                   ========================================================= */

                model.TopCyclists = cyclistPoints
                    .Where(c => c.Value > 0)
                    .Select(c =>
                    {
                        // Wielrenner zoeken
                        var cyclist = stageResults
                            .Select(sr => sr.Cyclist)
                            .Concat(jerseys.Select(j => j.Cyclist))
                            .FirstOrDefault(x => x.Id == c.Key);

                        // Nieuwe top wielrenner maken
                        return new TopCyclistItem
                        {
                            Name = cyclist != null
                                ? cyclist.FirstName + " " + cyclist.LastName
                                : "Unknown cyclist",

                            Points = c.Value
                        };
                    })
                    .OrderByDescending(c => c.Points)
                    .ThenBy(c => c.Name)
                    .Take(5)
                    .ToList();

                /* =========================================================
                   SPELERS EN SELECTIES OPHALEN
                   ========================================================= */

                var playerSelections = await _context.PlayerSelections
                    .Include(ps => ps.Player)
                    .Include(ps => ps.Cyclist)
                    .Where(ps => ps.GameSessionId == gameId)
                    .ToListAsync();

                /* =========================================================
                   SPELERS RANKING BEREKENEN
                   ========================================================= */

                model.PlayerRanking = playerSelections
                    .GroupBy(ps => ps.Player)
                    .Select(group => new PlayerRankingItem
                    {
                        // Naam van speler
                        PlayerName = group.Key.Name,

                        // Punten van alle gekozen renners optellen
                        Points = group.Sum(ps =>
                            cyclistPoints.ContainsKey(ps.CyclistId)
                                ? cyclistPoints[ps.CyclistId]
                                : 0)
                    })
                    .OrderByDescending(p => p.Points)
                    .ThenBy(p => p.PlayerName)
                    .ToList();

                // Positie nummers toevoegen
                for (int i = 0; i < model.PlayerRanking.Count; i++)
                {
                    model.PlayerRanking[i].Position = i + 1;
                }

                /* =========================================================
                   TRUIEN VOOR DASHBOARD
                   ========================================================= */

                model.Jerseys = jerseys
                    .OrderByDescending(j => j.Stage.StageNumber)
                    .Take(4)
                    .Select(j => new JerseyItem
                    {
                        Type = j.Type,

                        CyclistName = j.Cyclist.FirstName + " " + j.Cyclist.LastName
                    })
                    .ToList();

                /* =========================================================
                   LAATSTE ETAPPE RESULTATEN
                   ========================================================= */

                var latestStageWithResults = stageResults
                    .OrderByDescending(r => r.Stage.StageNumber)
                    .Select(r => r.Stage)
                    .FirstOrDefault();

                // Indien er resultaten bestaan
                if (latestStageWithResults != null)
                {
                    // Titel van laatste etappe
                    model.LatestStageTitle =
                        "Stage " +
                        latestStageWithResults.StageNumber +
                        " - " +
                        latestStageWithResults.Name;

                    // Top 3 van laatste etappe ophalen
                    model.LatestStageTop3 = stageResults
                        .Where(r =>
                            r.StageId == latestStageWithResults.Id &&
                            r.Position != null)
                        .OrderBy(r => r.Position)
                        .Take(3)
                        .Select(r =>
                            r.Position + ". " +
                            r.Cyclist.FirstName + " " +
                            r.Cyclist.LastName)
                        .ToList();
                }
            }
            catch
            {
                // Indien database offline is
                ViewBag.DatabaseOnline = false;
            }

            // Dashboard view teruggeven
            return View(model);
        }

        /* =========================================================
           HULPFUNCTIE VOOR TRUI TYPES
           Zet eenvoudige namen om naar database types
           ========================================================= */

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