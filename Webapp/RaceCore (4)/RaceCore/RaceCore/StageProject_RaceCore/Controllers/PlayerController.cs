using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class PlayerController : Controller
    {
        private readonly AppDbContext _context;

        public PlayerController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string searchTerm = "")
        {
            try
            {
                var playersQuery = _context.Players
                    .Select(p => new PlayerIndexViewModel
                    {
                        Id = p.Id,
                        Name = p.Name,
                        PositionInDraft = p.PositionInDraft,
                        TotalPoints = p.TotalPoints,
                        SelectionsCount = p.Selections.Count(),
                        DraftTurnsCount = p.DraftTurns.Count(),
                        PointsRecordsCount = p.PlayerPoints.Count()
                    });

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    playersQuery = playersQuery.Where(p => p.Name.Contains(searchTerm));
                }

                var players = await playersQuery
                    .OrderBy(p => p.PositionInDraft)
                    .ThenBy(p => p.Name)
                    .ToListAsync();

                ViewBag.DatabaseOnline = true;

                return View(new PlayerPageViewModel
                {
                    SearchTerm = searchTerm,
                    Players = players,
                    TotalPlayers = players.Count,
                    TotalPoints = players.Sum(p => p.TotalPoints),
                    TotalSelections = players.Sum(p => p.SelectionsCount),
                    TotalDraftTurns = players.Sum(p => p.DraftTurnsCount),
                    TotalPointRecords = players.Sum(p => p.PointsRecordsCount)
                });
            }
            catch
            {
                ViewBag.DatabaseOnline = false;
                TempData["DatabaseError"] = "Database unavailable. Start OpenVPN to view live players.";

                return View(new PlayerPageViewModel
                {
                    SearchTerm = searchTerm,
                    Players = new List<PlayerIndexViewModel>(),
                    TotalPlayers = 0,
                    TotalPoints = 0,
                    TotalSelections = 0,
                    TotalDraftTurns = 0,
                    TotalPointRecords = 0
                });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var player = await _context.Players
                    .Include(p => p.Selections)
                    .Include(p => p.DraftTurns)
                    .Include(p => p.PlayerPoints)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (player == null)
                {
                    TempData["Error"] = "Player not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(player);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not open player details: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        public IActionResult Create()
        {
            return View(new Player());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Player player)
        {
            if (!ModelState.IsValid)
            {
                return View(player);
            }

            try
            {
                _context.Players.Add(player);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Player added successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Player could not be added: " + ex.Message;
                return View(player);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var player = await _context.Players.FindAsync(id);

                if (player == null)
                {
                    TempData["Error"] = "Player not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(player);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not open edit page: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Player updatedPlayer)
        {
            if (id != updatedPlayer.Id)
            {
                TempData["Error"] = "Invalid player.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                return View(updatedPlayer);
            }

            try
            {
                var existingPlayer = await _context.Players.FindAsync(id);

                if (existingPlayer == null)
                {
                    TempData["Error"] = "Player not found.";
                    return RedirectToAction(nameof(Index));
                }

                existingPlayer.Name = updatedPlayer.Name;
                existingPlayer.PositionInDraft = updatedPlayer.PositionInDraft;
                existingPlayer.TotalPoints = updatedPlayer.TotalPoints;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Player updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Player could not be updated: " + ex.Message;
                return View(updatedPlayer);
            }
        }

        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var player = await _context.Players
                    .Include(p => p.Selections)
                    .Include(p => p.DraftTurns)
                    .Include(p => p.PlayerPoints)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (player == null)
                {
                    TempData["Error"] = "Player not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(player);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not open delete page: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var player = await _context.Players
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (player == null)
                {
                    TempData["Error"] = "Player not found.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Players.Remove(player);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Player and all linked data were successfully removed.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Player could not be removed: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}