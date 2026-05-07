using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    /* HistoryController.cs
       Purpose: Produce historical stage summaries and aggregated points per race.
       The controller selects the appropriate race and computes per-stage
       winners and a simple points aggregation for display in the History view.
    */
    /// <summary>
    /// Controller responsible for rendering historical race and stage overviews.
    /// </summary>
    public class HistoryController : Controller
    {
        private readonly AppDbContext _context;

        public HistoryController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> History(int? raceId)
        {
            // 1. Haal alle races op voor de dropdown
            var allRaces = await _context.Races.OrderByDescending(r => r.Year).ToListAsync();
            ViewBag.Races = allRaces;

            Race race = null;

            // 2. Bepaal welke race getoond moet worden (Automatische selectie logica)
            if (raceId.HasValue)
            {
                // Als de gebruiker zelf een race kiest uit de dropdown
                race = allRaces.FirstOrDefault(r => r.Id == raceId.Value);
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
                    race = activeGame.Race;
                }

                // Optie B (Fallback): Als er geen actieve game is, pak de race die vandaag bezig is
                if (race == null)
                {
                    race = allRaces.FirstOrDefault(r => r.StartDate <= DateTime.Now && r.EndDate >= DateTime.Now);
                }

                // Optie C (Laatste redmiddel): Pak de nieuwste race op basis van datum
                if (race == null)
                {
                    race = allRaces.OrderByDescending(r => r.StartDate).FirstOrDefault();
                }
            }

            if (race == null)
            {
                return NotFound("Geen race gevonden in de database.");
            }

            // Zorg dat de dropdown de juiste race als geselecteerd markeert
            ViewBag.SelectedRaceId = race.Id;

            // 3. Haal de etappes en puntenregels op voor de geselecteerde race
            var stages = await _context.Stages
                .Where(s => s.RaceId == race.Id)
                .OrderBy(s => s.StageNumber)
                .ToListAsync();

            var rules = await _context.PointsRules.ToListAsync();
            var stageHistoryItems = new List<StageHistoryItem>();

            // 4. Loop door de etappes voor de live puntenberekening
            foreach (var s in stages)
            {
                // Zoek de winnaar van de rit
                var winner = await _context.StageResults
                    .Include(sr => sr.Cyclist)
                    .ThenInclude(c => c.Team)
                    .FirstOrDefaultAsync(sr => sr.StageId == s.Id && sr.Position == 1);

                int totalPoints = 0;

                if (winner != null)
                {
                    // A. Bereken rit-punten (bijv. 100 voor positie 1)
                    int posPoints = rules
                        .Where(r => r.Type == "Rit" && r.FromPosition <= 1 && r.ToPosition >= 1)
                        .Sum(r => r.Points);

                    // B. Bereken trui-punten (bijv. 10 voor de rode trui)
                    var jerseys = await _context.Jerseys
                        .Where(j => j.StageId == s.Id && j.CyclistId == winner.CyclistId)
                        .ToListAsync();

                    int jerseyPoints = 0;
                    foreach (var j in jerseys)
                    {
                        string ruleType = j.Type switch
                        {
                            "Red" => "RodeTrui",
                            "Green" => "GroeneTrui",
                            "Blue" => "BlauweTrui",
                            "White" => "WitteTrui",
                            _ => j.Type
                        };
                        jerseyPoints += rules.Where(r => r.Type == ruleType).Sum(r => r.Points);
                    }

                    totalPoints = posPoints + jerseyPoints;
                }

                stageHistoryItems.Add(new StageHistoryItem
                {
                    StageId = s.Id,
                    StageNumber = s.StageNumber,
                    StageName = s.Name,
                    Date = s.Date,
                    WinnerName = winner?.Cyclist?.FullName ?? "Nog onbekend",
                    WinnerTeam = winner?.Cyclist?.Team?.Name ?? "-",
                    TopPlayerPoints = totalPoints,
                    TopPlayerName = "Punten"
                });
            }

            var model = new HistoryViewModel
            {
                RaceId = race.Id,
                RaceName = race.Name,
                Stages = stageHistoryItems
            };

            // Controleer of de database bereikbaar is voor de waarschuwingsbalk
            ViewBag.DatabaseOnline = await _context.Database.CanConnectAsync();

            return View(model);
        }
    }
}
