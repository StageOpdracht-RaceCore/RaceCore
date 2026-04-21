using Microsoft.AspNetCore.Mvc;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class PlayerController : Controller
    {
        private readonly AppDbContext _context;

        public PlayerController(AppDbContext context)
        {
            _context = context;
        }

        // LIST
        public IActionResult Index()
        {
            var players = _context.Players
                .OrderBy(p => p.PositionInDraft)
                .ToList();

            return View(players);
        }

        // DETAILS
        public IActionResult Details(int id)
        {
            var player = _context.Players.FirstOrDefault(p => p.Id == id);

            if (player == null)
                return NotFound();

            return View(player);
        }

        // CREATE (GET)
        public IActionResult Create()
        {
            return View();
        }

        // CREATE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Player player)
        {
            if (ModelState.IsValid)
            {
                _context.Players.Add(player);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            return View(player);
        }

        // EDIT (GET)
        public IActionResult Edit(int id)
        {
            var player = _context.Players.Find(id);

            if (player == null)
                return NotFound();

            return View(player);
        }

        // EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Player player)
        {
            if (ModelState.IsValid)
            {
                _context.Players.Update(player);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            return View(player);
        }

        // DELETE (GET)
        public IActionResult Delete(int id)
        {
            var player = _context.Players.Find(id);

            if (player == null)
                return NotFound();

            return View(player);
        }

        // DELETE (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var player = _context.Players.Find(id);

            if (player != null)
            {
                _context.Players.Remove(player);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}