using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class GameController : Controller
    {
        private readonly AppDbContext _context;

        public GameController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult New()
        {
            return View(new NewGameViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(NewGameViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var playerNames = model.PlayerNamesRaw
                .Split(new[] { "\r\n", "\n", ",", ";" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (playerNames.Count < 2)
            {
                ModelState.AddModelError(nameof(model.PlayerNamesRaw), "Voeg minstens 2 spelers toe.");
                return View(model);
            }

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                _context.PlayerPoints.RemoveRange(_context.PlayerPoints);
                _context.PlayerSelections.RemoveRange(_context.PlayerSelections);
                _context.DraftTurns.RemoveRange(_context.DraftTurns);
                _context.Jerseys.RemoveRange(_context.Jerseys);
                _context.StageResults.RemoveRange(_context.StageResults);
                _context.Stages.RemoveRange(_context.Stages);
                _context.RaceEntries.RemoveRange(_context.RaceEntries);
                _context.Races.RemoveRange(_context.Races);
                _context.Players.RemoveRange(_context.Players);

                await _context.SaveChangesAsync();

                var race = new Race
                {
                    Name = model.RaceName,
                    Year = model.Year,
                    StartDate = DateTime.Today,
                    EndDate = DateTime.Today.AddDays(model.NumberOfStages - 1)
                };

                _context.Races.Add(race);
                await _context.SaveChangesAsync();

                for (int i = 1; i <= model.NumberOfStages; i++)
                {
                    _context.Stages.Add(new Stage
                    {
                        RaceId = race.Id,
                        StageNumber = i,
                        Name = $"Rit {i}",
                        Date = DateTime.Today.AddDays(i - 1)
                    });
                }

                int position = 1;

                foreach (var name in playerNames)
                {
                    _context.Players.Add(new Player
                    {
                        Name = name,
                        PositionInDraft = position,
                        TotalPoints = 0
                    });

                    position++;
                }

                await _context.SaveChangesAsync();

                var players = await _context.Players
                    .OrderBy(p => p.PositionInDraft)
                    .ThenBy(p => p.Id)
                    .ToListAsync();

                const int totalRounds = 15;
                var draftTurns = new List<DraftTurn>();
                int turnNumber = 1;
                int playerCount = players.Count;

                for (int round = 1; round <= totalRounds; round++)
                {
                    int pairIndex = (round - 1) / 2;
                    int startIndex = pairIndex % playerCount;

                    var roundPlayers = players
                        .Skip(startIndex)
                        .Concat(players.Take(startIndex))
                        .ToList();

                    if (round % 2 == 0)
                    {
                        roundPlayers.Reverse();
                    }

                    foreach (var player in roundPlayers)
                    {
                        draftTurns.Add(new DraftTurn
                        {
                            RaceId = race.Id,
                            PlayerId = player.Id,
                            TurnNumber = turnNumber
                        });

                        turnNumber++;
                    }
                }

                _context.DraftTurns.AddRange(draftTurns);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["Success"] = "Nieuwe game gestart. De snake draft is klaar.";
                return RedirectToAction("Index", "Draft", new { raceId = race.Id });
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
                return View(model);
            }
        }
    }
}