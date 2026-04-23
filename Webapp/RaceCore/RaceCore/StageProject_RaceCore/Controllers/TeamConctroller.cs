using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class TeamController : Controller
    {
        private readonly AppDbContext _context;

        public TeamController(AppDbContext context)
        {
            _context = context;
        }

        // Loads teams with their cyclists from the database and passes a view model to the view.
        public async Task<IActionResult> Index()
        {
            try
            {
                // Return an empty page when the database is unavailable.
                if (!await _context.Database.CanConnectAsync())
                {
                    return View(new TeamIndexViewModel());
                }

                var teams = await _context.Teams
                    .OrderBy(t => t.Name)
                    .Select(t => new TeamViewModel
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Tag = t.Tag,
                        ActiveCyclistsCount = t.Cyclists.Count(c => c.IsActive),
                        BenchCyclistsCount = t.Cyclists.Count(c => !c.IsActive),
                        ActiveCyclists = t.Cyclists
                            .Where(c => c.IsActive)
                            .Select(c => new CyclistSimple
                            {
                                Id = c.Id,
                                FirstName = c.FirstName,
                                LastName = c.LastName,
                                IsActive = c.IsActive
                            })
                            .ToList(),
                        BenchCyclists = t.Cyclists
                            .Where(c => !c.IsActive)
                            .Select(c => new CyclistSimple
                            {
                                Id = c.Id,
                                FirstName = c.FirstName,
                                LastName = c.LastName,
                                IsActive = c.IsActive
                            })
                            .ToList()
                    })
                    .ToListAsync();

                var availableCyclists = await _context.Cyclists
                    .Where(c => c.TeamId == null)
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .ToListAsync();

                return View(new TeamIndexViewModel
                {
                    Teams = teams,
                    AvailableCyclists = availableCyclists
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return View(new TeamIndexViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCyclistToTeam(int teamId, int cyclistId, bool isActive)
        {
            try
            {
                var team = await _context.Teams.FindAsync(teamId);
                if (team == null)
                {
                    return NotFound();
                }

                var cyclist = await _context.Cyclists.FindAsync(cyclistId);
                if (cyclist == null)
                {
                    return NotFound();
                }

                if (cyclist.TeamId.HasValue && cyclist.TeamId != teamId)
                {
                    return RedirectToAction(nameof(Index));
                }

                cyclist.TeamId = teamId;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCyclistFromTeam(int cyclistId)
        {
            try
            {
                var cyclist = await _context.Cyclists.FindAsync(cyclistId);
                if (cyclist == null)
                {
                    return NotFound();
                }

                cyclist.TeamId = null;
                cyclist.IsActive = false;

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
