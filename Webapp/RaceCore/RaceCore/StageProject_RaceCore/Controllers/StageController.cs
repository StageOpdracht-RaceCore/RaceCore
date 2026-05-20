using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class StageController : Controller
    {
        private readonly AppDbContext _context;

        public StageController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? raceId, int? gameId)
        {
            return RedirectToAction("StageResults", "Result", new
            {
                raceId = raceId,
                gameId = gameId
            });
        }

        public async Task<IActionResult> Details(int id)
        {
            return RedirectToAction("StageResults", "Result");
        }
    }
}