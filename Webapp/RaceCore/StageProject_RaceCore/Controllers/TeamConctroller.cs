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

        // Loads teams with their cyclists from the database and passes them to the view
        public async Task<IActionResult> Index()
        {
            try
            {
                // Ensure the database is reachable before querying
                if (!await _context.Database.CanConnectAsync())
                {
                    // Return empty list to the view when DB is not available
                    return View(new List<Team>());
                }

                var teams = await _context.Teams
                    .Include(t => t.Cyclists)
                    .OrderBy(t => t.Name)
                    .ToListAsync();

                return View(teams);
            }
            catch (Exception ex)
            {
                // Minimal logging and return an empty model to avoid crashing the app
                Console.WriteLine(ex);
                return View(new List<Team>());
            }
        }
    }
}

