using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class DraftController : Controller
    {
        private readonly AppDbContext _context;

        public DraftController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int raceId)
        {
            ViewBag.Cyclists = new List<Cyclist>();
            ViewBag.RaceId = raceId;
            ViewBag.DatabaseOnline = false;
            ViewBag.NoDraft = false;
            ViewBag.PlayerCount = 0;

            try
            {
                if (raceId <= 0)
                {
                    var firstRace = await _context.Races
                        .OrderByDescending(r => r.Year)
                        .ThenBy(r => r.Name)
                        .FirstOrDefaultAsync();

                    if (firstRace == null)
                    {
                        TempData["Error"] = "Er is nog geen race aangemaakt. Maak eerst een race aan.";
                        return View(new List<DraftTurnViewModel>());
                    }

                    raceId = firstRace.Id;
                }

                var players = await _context.Players
                    .OrderBy(p => p.PositionInDraft)
                    .ThenBy(p => p.Id)
                    .ToListAsync();

                var draftTurnsDb = await _context.DraftTurns
                    .Where(d => d.RaceId == raceId)
                    .Include(d => d.Player)
                    .Include(d => d.Cyclist)
                    .OrderBy(d => d.TurnNumber)
                    .ToListAsync();

                var cyclists = await _context.Cyclists
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .ToListAsync();

                var viewModel = draftTurnsDb.Select(d => new DraftTurnViewModel
                {
                    Id = d.Id,
                    TurnNumber = d.TurnNumber,
                    PlayerName = d.Player != null ? d.Player.Name : "Unknown",
                    CyclistId = d.CyclistId,
                    CyclistName = d.Cyclist != null ? d.Cyclist.FullName : null
                }).ToList();

                ViewBag.Cyclists = cyclists;
                ViewBag.RaceId = raceId;
                ViewBag.DatabaseOnline = true;
                ViewBag.NoDraft = !draftTurnsDb.Any();
                ViewBag.PlayerCount = players.Count;

                return View(viewModel);
            }
            catch
            {
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om live draft gegevens te zien.";
                return View(new List<DraftTurnViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateDraft(int raceId)
        {
            try
            {
                if (raceId <= 0)
                {
                    TempData["Error"] = "Geen geldige race gevonden.";
                    return RedirectToAction("Index");
                }

                var raceExists = await _context.Races.AnyAsync(r => r.Id == raceId);

                if (!raceExists)
                {
                    TempData["Error"] = "Race niet gevonden.";
                    return RedirectToAction("Index");
                }

                var players = await _context.Players
                    .OrderBy(p => p.PositionInDraft)
                    .ThenBy(p => p.Id)
                    .ToListAsync();

                if (!players.Any())
                {
                    TempData["Error"] = "Er zijn nog geen spelers. Voeg eerst players toe voor je een draft genereert.";
                    return RedirectToAction("Index", new { raceId });
                }

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
                            RaceId = raceId,
                            PlayerId = player.Id,
                            TurnNumber = turnNumber
                        });

                        turnNumber++;
                    }
                }

                var existingSelections = await _context.PlayerSelections
                    .Where(ps => ps.RaceId == raceId)
                    .ToListAsync();

                var existingTurns = await _context.DraftTurns
                    .Where(dt => dt.RaceId == raceId)
                    .ToListAsync();

                _context.PlayerSelections.RemoveRange(existingSelections);
                _context.DraftTurns.RemoveRange(existingTurns);

                await _context.SaveChangesAsync();

                _context.DraftTurns.AddRange(draftTurns);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Fair snake draft succesvol gegenereerd.";
                return RedirectToAction("Index", new { raceId });
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
                return RedirectToAction("Index", new { raceId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PickCyclist(int draftTurnId, int cyclistId, int raceId)
        {
            try
            {
                if (cyclistId <= 0)
                {
                    TempData["Error"] = "Kies eerst een geldige renner.";
                    return RedirectToAction("Index", new { raceId });
                }

                var turn = await _context.DraftTurns
                    .Include(t => t.Player)
                    .FirstOrDefaultAsync(t => t.Id == draftTurnId && t.RaceId == raceId);

                if (turn == null)
                {
                    TempData["Error"] = "Draft beurt niet gevonden.";
                    return RedirectToAction("Index", new { raceId });
                }

                if (turn.CyclistId.HasValue)
                {
                    TempData["Error"] = "Voor deze beurt is al een renner gekozen.";
                    return RedirectToAction("Index", new { raceId });
                }

                var currentTurn = await _context.DraftTurns
                    .Where(t => t.RaceId == raceId && t.CyclistId == null)
                    .OrderBy(t => t.TurnNumber)
                    .FirstOrDefaultAsync();

                if (currentTurn == null)
                {
                    TempData["Error"] = "De draft is al afgerond.";
                    return RedirectToAction("Index", new { raceId });
                }

                if (currentTurn.Id != draftTurnId)
                {
                    TempData["Error"] = "Je kan alleen kiezen voor de huidige beurt.";
                    return RedirectToAction("Index", new { raceId });
                }

                bool alreadyPicked = await _context.DraftTurns
                    .AnyAsync(t => t.RaceId == raceId && t.CyclistId == cyclistId);

                if (alreadyPicked)
                {
                    TempData["Error"] = "Deze renner is al gekozen.";
                    return RedirectToAction("Index", new { raceId });
                }

                var cyclistExists = await _context.Cyclists
                    .AnyAsync(c => c.Id == cyclistId && c.IsActive);

                if (!cyclistExists)
                {
                    TempData["Error"] = "De gekozen renner bestaat niet of is niet actief.";
                    return RedirectToAction("Index", new { raceId });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                turn.CyclistId = cyclistId;

                int currentPlayerPickCount = await _context.PlayerSelections
                    .CountAsync(ps => ps.RaceId == raceId && ps.PlayerId == turn.PlayerId);

                var selectionExists = await _context.PlayerSelections
                    .AnyAsync(ps =>
                        ps.RaceId == raceId &&
                        ps.PlayerId == turn.PlayerId &&
                        ps.CyclistId == cyclistId);

                if (!selectionExists)
                {
                    _context.PlayerSelections.Add(new PlayerSelection
                    {
                        RaceId = raceId,
                        PlayerId = turn.PlayerId,
                        CyclistId = cyclistId,
                        IsActive = currentPlayerPickCount < 10
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"{turn.Player.Name} heeft een renner gekozen.";
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
            }

            return RedirectToAction("Index", new { raceId });
        }
    }
}