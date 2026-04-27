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

        public async Task<IActionResult> Index(int? raceId)
        {
            var races = await _context.Races
                .OrderBy(r => r.Name)
                .ToListAsync();

            if (!races.Any())
            {
                ViewBag.Races = races;
                ViewBag.SelectedRaceId = 0;
                return View(new List<Stage>());
            }

            int selectedRaceId = raceId ?? races.First().Id;

            var stages = await _context.Stages
                .Where(s => s.RaceId == selectedRaceId)
                .OrderBy(s => s.StageNumber)
                .ToListAsync();

            ViewBag.Races = races;
            ViewBag.SelectedRaceId = selectedRaceId;

            return View(stages);
        }
    }
}