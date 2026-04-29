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
            ViewBag.SelectedRaceId = raceId;

            return View(stages);
        }

        // 👉 NIEUW: Details pagina
        public async Task<IActionResult> Details(int id)
        {
            var stage = await _context.Stages
                .Include(s => s.Race)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (stage == null)
                return NotFound();

            var results = await _context.StageResults
                .Include(r => r.Cyclist)
                .Where(r => r.StageId == id)
                .ToListAsync();

            var top25 = results
                .Where(r => r.Position > 0 && r.Position <= 25)
                .OrderBy(r => r.Position)
                .ToList();

            var jerseys = results
                .Where(r => r.Position == 0)
                .ToList();

            ViewBag.Top25 = top25;
            ViewBag.Jerseys = jerseys;

            return View(stage);
        }
    }
}