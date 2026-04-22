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

        // INDEX
        public async Task<IActionResult> Index(string searchTerm = "")
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

            var summary = new PlayerPageViewModel
            {
                SearchTerm = searchTerm,
                Players = players,
                TotalPlayers = players.Count,
                TotalPoints = players.Sum(p => p.TotalPoints),
                TotalSelections = players.Sum(p => p.SelectionsCount),
                TotalDraftTurns = players.Sum(p => p.DraftTurnsCount),
                TotalPointRecords = players.Sum(p => p.PointsRecordsCount)
            };

            return View(summary);
        }

        // DETAILS
        public async Task<IActionResult> Details(int id)
        {
            var player = await _context.Players
                .Include(p => p.Selections)
                    .ThenInclude(s => s.Cyclist)
                .Include(p => p.Selections)
                    .ThenInclude(s => s.Race)
                .Include(p => p.DraftTurns)
                    .ThenInclude(d => d.Race)
                .Include(p => p.PlayerPoints)
                    .ThenInclude(pp => pp.Race)
                .Include(p => p.PlayerPoints)
                    .ThenInclude(pp => pp.Stage)
                .Include(p => p.PlayerPoints)
                    .ThenInclude(pp => pp.Cyclist)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (player == null)
                return NotFound();

            return View(player);
        }

        // CREATE (GET)
        public IActionResult Create()
        {
            return View(new Player());
        }

        // CREATE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Player player)
        {
            if (!ModelState.IsValid)
                return View(player);

            _context.Players.Add(player);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // EDIT (GET)
        public async Task<IActionResult> Edit(int id)
        {
            var player = await _context.Players.FindAsync(id);

            if (player == null)
                return NotFound();

            return View(player);
        }

        // EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Player updatedPlayer)
        {
            if (id != updatedPlayer.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return View(updatedPlayer);

            var existingPlayer = await _context.Players.FindAsync(id);

            if (existingPlayer == null)
                return NotFound();

            existingPlayer.Name = updatedPlayer.Name;
            existingPlayer.PositionInDraft = updatedPlayer.PositionInDraft;
            existingPlayer.TotalPoints = updatedPlayer.TotalPoints;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // DELETE (GET)
        public async Task<IActionResult> Delete(int id)
        {
            var player = await _context.Players
                .FirstOrDefaultAsync(p => p.Id == id);

            if (player == null)
                return NotFound();

            return View(player);
        }

        // DELETE (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var player = await _context.Players.FindAsync(id);

            if (player != null)
            {
                _context.Players.Remove(player);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}