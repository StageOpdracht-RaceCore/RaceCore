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

        // =========================
        // DRAFT OVERVIEW
        // =========================
        public async Task<IActionResult> Index(int raceId)
        {
            var draftTurnsDb = await _context.DraftTurns
                .Where(d => d.RaceId == raceId)
                .Include(d => d.Player)
                .Include(d => d.Cyclist)
                .ToListAsync();

            var cyclists = await _context.Cyclists.ToListAsync();

            List<DraftTurnViewModel> viewModel;

            // =========================
            // TEMP FALLBACK (als DB leeg is)
            // =========================
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
                    PlayerName = d.Player?.Name ?? "Unknown",
                    CyclistId = d.CyclistId,
                    CyclistName = d.Cyclist != null ? d.Cyclist.FullName : null
                }).ToList();
            }

            ViewBag.Cyclists = cyclists;
            ViewBag.RaceId = raceId;

            return View(viewModel);
        }

        // =========================
        // PICK CYCLIST
        // =========================
        [HttpPost]
        public async Task<IActionResult> PickCyclist(int draftTurnId, int cyclistId, int raceId)
        {
            var turn = await _context.DraftTurns
                .FirstOrDefaultAsync(t => t.Id == draftTurnId);

            if (turn == null)
                return NotFound();

            // check dubbele pick
            bool alreadyPicked = await _context.DraftTurns
                .AnyAsync(t => t.CyclistId == cyclistId && t.RaceId == raceId);

            if (alreadyPicked)
            {
                TempData["Error"] = "Renner al gekozen!";
                return RedirectToAction("Index", new { raceId });
            }

            turn.CyclistId = cyclistId;

            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { raceId });
        }
    }
}