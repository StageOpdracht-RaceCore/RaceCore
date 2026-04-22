using Microsoft.AspNetCore.Mvc;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class PlayerController : Controller
    {
        // tijdelijke opslag (mock)
        private static List<Player> players = new List<Player>
        {
            new Player { Id = 1, Name = "Roel" },
            new Player { Id = 2, Name = "Casper" },
            new Player { Id = 3, Name = "Jonas" }
        };

        // INDEX
        public IActionResult Index()
        {
            return View(players);
        }

        // DETAILS
        public IActionResult Details(int id)
        {
            var player = players.FirstOrDefault(p => p.Id == id);
            if (player == null) return NotFound();

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
                player.Id = players.Any() ? players.Max(p => p.Id) + 1 : 1;
                players.Add(player);
                return RedirectToAction(nameof(Index));
            }

            return View(player);
        }

        // EDIT (GET)
        public IActionResult Edit(int id)
        {
            var player = players.FirstOrDefault(p => p.Id == id);
            if (player == null) return NotFound();

            return View(player);
        }

        // EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Player updatedPlayer)
        {
            if (id != updatedPlayer.Id) return NotFound();

            var player = players.FirstOrDefault(p => p.Id == id);
            if (player == null) return NotFound();

            if (ModelState.IsValid)
            {
                player.Name = updatedPlayer.Name;
                // relaties niet overschrijven hier (Selections etc.)
                return RedirectToAction(nameof(Index));
            }

            return View(updatedPlayer);
        }

        // DELETE (GET)
        public IActionResult Delete(int id)
        {
            var player = players.FirstOrDefault(p => p.Id == id);
            if (player == null) return NotFound();

            return View(player);
        }

        // DELETE (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var player = players.FirstOrDefault(p => p.Id == id);
            if (player != null)
            {
                players.Remove(player);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}