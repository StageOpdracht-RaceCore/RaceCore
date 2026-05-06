using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class HistoryController : Controller
    {
        private readonly AppDbContext _context;

        public HistoryController(AppDbContext context)
        {
            _context = context;
        }

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
                    // A. Bereken rit-punten (bijv. 100 voor Piet)
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