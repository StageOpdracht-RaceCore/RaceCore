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

            try
            {
                var draftTurnsDb = await _context.DraftTurns
                    .Where(d => d.RaceId == raceId)
                    .Include(d => d.Player)
                    .Include(d => d.Cyclist)
                    .OrderBy(d => d.TurnNumber)
                    .ToListAsync();

                var cyclists = await _context.Cyclists
                    .OrderBy(c => c.FirstName)
                    .ThenBy(c => c.LastName)
                    .ToListAsync();

                List<DraftTurnViewModel> viewModel;

                if (!draftTurnsDb.Any())
                {
                    viewModel = new List<DraftTurnViewModel>
                    {
                        new DraftTurnViewModel { Id = 1, TurnNumber = 1, PlayerName = "Roel" },
                        new DraftTurnViewModel { Id = 2, TurnNumber = 2, PlayerName = "Casper" },
                        new DraftTurnViewModel { Id = 3, TurnNumber = 3, PlayerName = "Jonas" },
                        new DraftTurnViewModel { Id = 4, TurnNumber = 4, PlayerName = "Dries" }
                    };
                }
                else
                {
                    viewModel = draftTurnsDb.Select(d => new DraftTurnViewModel
                    {
                        Id = d.Id,
                        TurnNumber = d.TurnNumber,
                        PlayerName = d.Player != null ? d.Player.Name : "Unknown",
                        CyclistId = d.CyclistId,
                        CyclistName = d.Cyclist != null ? d.Cyclist.FullName : null
                    }).ToList();
                }

                ViewBag.Cyclists = cyclists;
                ViewBag.RaceId = raceId;
                ViewBag.DatabaseOnline = true;

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
                    TempData["Error"] = "Renner al gekozen!";
                    return RedirectToAction("Index", new { raceId });
                }

                var cyclistExists = await _context.Cyclists.AnyAsync(c => c.Id == cyclistId);

                if (!cyclistExists)
                {
                    TempData["Error"] = "De gekozen renner bestaat niet.";
                    return RedirectToAction("Index", new { raceId });
                }

                turn.CyclistId = cyclistId;
                await _context.SaveChangesAsync();
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
            }

            return RedirectToAction("Index", new { raceId });
        }
    }
}
