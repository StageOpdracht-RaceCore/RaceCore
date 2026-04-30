using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class MapController : Controller
    {
        private readonly AppDbContext _context;

        public MapController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int raceId = 0)
        {
            var races = await _context.Races
                .OrderByDescending(r => r.Year)
                .ThenBy(r => r.Name)
                .ToListAsync();

            if (raceId <= 0 && races.Any())
            {
                raceId = races.First().Id;
            }

            var stages = await _context.Stages
                .Include(s => s.Race)
                .Where(s => s.RaceId == raceId)
                .OrderBy(s => s.StageNumber)
                .ToListAsync();

            ViewBag.Races = races;
            ViewBag.RaceId = raceId;

            return View(stages);
        }
    }
}