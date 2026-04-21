using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class CyclistController : Controller
    {
        private readonly AppDbContext _context;

        public CyclistController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? searchString, string? activeFilter)
        {
            var cyclists = new List<Cyclist>
            {
                new Cyclist
                {
                    Id = 1,
                    FirstName = "Wout",
                    LastName = "Van Aert",
                    IsActive = true,
                    Team = new Team { Name = "Visma | Lease a Bike" }
                },
                new Cyclist
                {
                    Id = 2,
                    FirstName = "Tadej",
                    LastName = "Pogacar",
                    IsActive = true,
                    Team = new Team { Name = "UAE Team Emirates" }
                },
                new Cyclist
                {
                    Id = 3,
                    FirstName = "Tom",
                    LastName = "Dumoulin",
                    IsActive = false,
                    Team = new Team { Name = "Retired" }
                },
                new Cyclist
                {
                    Id = 4,
                    FirstName = "Bart",
                    LastName = "Nogiets",
                    IsActive = true,
                    Team = new Team { Name = "OlaOle Vietske" }
                }
            };

            // HARD CODED LIJST FILTEREN
            var query = cyclists.AsQueryable();

            // Zoeken op voornaam, achternaam of team
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(c =>
                    c.FirstName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                    c.LastName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                    (c.Team != null && c.Team.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase)));
            }

            // Filter op IsActive
            if (!string.IsNullOrWhiteSpace(activeFilter))
            {
                switch (activeFilter.ToLower())
                {
                    case "yes":
                        query = query.Where(c => c.IsActive);
                        break;

                    case "no":
                        query = query.Where(c => !c.IsActive);
                        break;
                }
            }

            ViewBag.SearchString = searchString;
            ViewBag.ActiveFilter = activeFilter;

            var filteredCyclists = query
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToList();

            return View(filteredCyclists);

            /*
            // TOEKOMSTIGE DATABASECODE
            var query = _context.Cyclists
                .Include(c => c.Team)
                .AsQueryable();

            // Zoeken op voornaam, achternaam of team
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(c =>
                    c.FirstName.Contains(searchString) ||
                    c.LastName.Contains(searchString) ||
                    (c.Team != null && c.Team.Name.Contains(searchString)));
            }

            // Filter op IsActive
            if (!string.IsNullOrWhiteSpace(activeFilter))
            {
                switch (activeFilter.ToLower())
                {
                    case "yes":
                        query = query.Where(c => c.IsActive);
                        break;

                    case "no":
                        query = query.Where(c => !c.IsActive);
                        break;
                }
            }

            var cyclistsFromDb = await query
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToListAsync();

            return View(cyclistsFromDb);
            */
        }
    }
}