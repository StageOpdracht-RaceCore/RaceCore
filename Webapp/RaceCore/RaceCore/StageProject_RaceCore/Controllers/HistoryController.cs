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
                // Toon de meest recente race als er geen ID is meegegeven
                race = allRaces.OrderByDescending(r => r.StartDate).FirstOrDefault();
            }

            if (race == null)
            {
                return NotFound("Geen race gevonden in de database.");
            }

            // 4. Bouw het model op met alle etappes en bijbehorende resultaten
            var model = new HistoryViewModel
            {
                RaceId = race.Id,
                RaceName = race.Name,
                Stages = await _context.Stages
                    .Where(s => s.RaceId == race.Id)
                    .OrderBy(s => s.StageNumber)
                    .Select(s => new StageHistoryItem
                    {
                        StageId = s.Id,
                        StageNumber = s.StageNumber,
                        StageName = s.Name,
                        Date = s.Date,

                        // Haal de winnaar van de etappe op (Position 1 in StageResults)
                        WinnerName = _context.StageResults
                            .Where(sr => sr.StageId == s.Id && sr.Position == 1)
                            .Select(sr => sr.Cyclist.FirstName + " " + sr.Cyclist.LastName)
                            .FirstOrDefault() ?? "Nog onbekend",

                        WinnerTeam = _context.StageResults
                            .Where(sr => sr.StageId == s.Id && sr.Position == 1)
                            .Select(sr => sr.Cyclist.Team.Name)
                            .FirstOrDefault() ?? "-",

                        // Haal de speler op met de hoogste score in deze specifieke etappe
                        TopPlayerName = _context.PlayerPoints
                            .Where(pp => pp.StageId == s.Id)
                            .GroupBy(pp => pp.Player.Name)
                            .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Points) })
                            .OrderByDescending(x => x.Total)
                            .Select(x => x.Name)
                            .FirstOrDefault() ?? "Geen data",

                        TopPlayerPoints = _context.PlayerPoints
                            .Where(pp => pp.StageId == s.Id)
                            .GroupBy(pp => pp.Player.Id)
                            .Select(g => g.Sum(x => x.Points))
                            .OrderByDescending(points => points)
                            .FirstOrDefault()
                    })
                    .ToListAsync()
            };

            // Controleer of de database bereikbaar is voor de waarschuwingsbalk
            ViewBag.DatabaseOnline = await _context.Database.CanConnectAsync();

            // Dit koppelt aan Views/History/History.cshtml
            return View(model);
        }
    }
}