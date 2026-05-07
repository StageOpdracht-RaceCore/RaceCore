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
        /// <summary>
        /// Databasecontext voor toegang tot de applicatiedatabase.
        /// </summary>
        private readonly AppDbContext _context;

        /// <summary>
        /// Initialiseert een nieuwe instantie van de HistoryController.
        /// </summary>
        /// <param name="context">
        /// Databasecontext die gebruikt wordt voor databankqueries.
        /// </param>
        public HistoryController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Toont het historiek-overzicht van een race.
        /// 
        /// Functionaliteiten:
        /// - ophalen van races voor de dropdown
        /// - selecteren van een race
        /// - ophalen van etappes
        /// - berekenen van rit- en truipunten
        /// - opbouwen van een HistoryViewModel
        /// - controleren van databaseconnectie
        /// </summary>
        /// <param name="raceId">
        /// Optionele ID van de geselecteerde race.
        /// Indien geen ID wordt meegegeven,
        /// wordt de meest recente race gebruikt.
        /// </param>
        /// <returns>
        /// Een View met het ingevulde HistoryViewModel.
        /// </returns>
        public async Task<IActionResult> History(int? raceId)
        {
            // 1. Haal alle races op uit de database voor de dropdown
            var allRaces = await _context.Races.OrderByDescending(r => r.Year).ToListAsync();

            // 2. Stop ze in de ViewBag zodat de View ze kan zien
            ViewBag.Races = allRaces;
            ViewBag.SelectedRaceId = raceId;

            // 3. Bepaal welke race getoond moet worden
            Race race;
            if (raceId.HasValue)
            {
                race = allRaces.FirstOrDefault(r => r.Id == raceId.Value);
            }
            else
            {
                race = allRaces.OrderByDescending(r => r.StartDate).FirstOrDefault();
            }

            if (race == null)
            {
                return NotFound("Geen race gevonden in de database.");
            }

            // 4. Haal de etappes en puntenregels op
            var stages = await _context.Stages
                .Where(s => s.RaceId == race.Id)
                .OrderBy(s => s.StageNumber)
                .ToListAsync();

            var rules = await _context.PointsRules.ToListAsync();
            var stageHistoryItems = new List<StageHistoryItem>();

            // 5. Loop door de etappes voor de live puntenberekening
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
                    // A. Bereken rit-punten
                    int posPoints = rules
                        .Where(r => r.Type == "Rit" && r.FromPosition <= 1 && r.ToPosition >= 1)
                        .Sum(r => r.Points);

                    // B. Bereken trui-punten
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

            /// <summary>
            /// ViewModel met alle gegevens voor de History View.
            /// </summary>
            var model = new HistoryViewModel
            {
                RaceId = race.Id,
                RaceName = race.Name,
                Stages = stageHistoryItems
            };

            /// <summary>
            /// Controleert of de database bereikbaar is.
            /// Wordt gebruikt voor een waarschuwingsmelding in de UI.
            /// </summary>
            ViewBag.DatabaseOnline = await _context.Database.CanConnectAsync();

            /// <summary>
            /// Geeft de View terug met het ingevulde model.
            /// </summary>
            return View(model);
        }
    }
}
