using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;

namespace StageProject_RaceCore.Controllers
{
    public class TeamController : Controller
    {
        private readonly AppDbContext _context;

        public TeamController(AppDbContext context)
        {
            _context = context;
        }

        // Loads teams with their cyclists from the database and passes a view model to the view
        public async Task<IActionResult> Index()
        {
            try
            {
                // Ensure the database is reachable before querying
                if (!await _context.Database.CanConnectAsync())
                {
                    // Return empty list to the view when DB is not available
                    return View(new List<TeamViewModel>());
                }

                // Project teams and composition information using database queries.
                var teams = await _context.Teams
                    .OrderBy(t => t.Name)
                    .Select(t => new TeamViewModel
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Tag = t.Tag,
                        ActiveCyclistsCount = t.Cyclists.Count(c => c.IsActive),
                        BenchCyclistsCount = t.Cyclists.Count(c => !c.IsActive),
                        ActiveCyclists = t.Cyclists.Where(c => c.IsActive).Select(c => new CyclistSimple { Id = c.Id, FirstName = c.FirstName, LastName = c.LastName, IsActive = c.IsActive }).ToList(),
                        BenchCyclists = t.Cyclists.Where(c => !c.IsActive).Select(c => new CyclistSimple { Id = c.Id, FirstName = c.FirstName, LastName = c.LastName, IsActive = c.IsActive }).ToList()
                    })
                    .ToListAsync();

                return View(teams);
            }
            catch (Exception ex)
            {
                // Minimal logging and return an empty model to avoid crashing the app
                Console.WriteLine(ex);
                return View(new List<TeamViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCyclistStatus(int cyclistId, bool isActive)
        {
            try
            {
                var cyclist = await _context.Cyclists.FindAsync(cyclistId);
                if (cyclist == null)
                {
                    return NotFound();
                }

                cyclist.IsActive = isActive;
                _context.Cyclists.Update(cyclist);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return RedirectToAction(nameof(Index));
            }
        }
    }
}

