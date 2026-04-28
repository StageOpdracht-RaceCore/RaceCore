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
            try
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
                ViewBag.DatabaseOnline = true;

                return View(stages);
            }
            catch
            {
                ViewBag.Races = new List<Race>();
                ViewBag.SelectedRaceId = 0;
                ViewBag.DatabaseOnline = false;
                TempData["DatabaseError"] = "Database niet bereikbaar.";
                return View(new List<Stage>());
            }
        }
    }
}