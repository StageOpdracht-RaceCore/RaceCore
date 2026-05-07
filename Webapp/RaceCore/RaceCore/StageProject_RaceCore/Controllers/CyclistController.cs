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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string firstName, string lastName, int? teamId, bool isActive = true, int? raceId = null)
        {
            if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
            {
                Race? selectedRace = null;

                if (raceId.HasValue && raceId.Value > 0)
                {
                    selectedRace = await _context.Races.FindAsync(raceId.Value);

                    if (selectedRace == null)
                    {
                        TempData["CreateError"] = "Geselecteerde race bestaat niet.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                var cyclist = new Cyclist
                {
                    FirstName = firstName.Trim(),
                    LastName = lastName.Trim(),
                    TeamId = teamId == 0 ? null : teamId,
                    IsActive = isActive
                };

                _context.Cyclists.Add(cyclist);
                await _context.SaveChangesAsync();

                if (selectedRace != null)
                {
                    _context.RaceEntries.Add(new RaceEntry
                    {
                        RaceId = selectedRace.Id,
                        CyclistId = cyclist.Id,
                        TeamId = cyclist.TeamId
                    });

                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = $"{cyclist.FullName} is succesvol toegevoegd.";
            }
            else
            {
                TempData["CreateError"] = "Voornaam en achternaam zijn verplicht.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToRace(
            int cyclistId,
            int raceId,
            string? search,
            string? status,
            int page = 1,
            int pageSize = 25)
        {
            var cyclist = await _context.Cyclists
                .Include(c => c.RaceEntries)
                .FirstOrDefaultAsync(c => c.Id == cyclistId);

            var race = await _context.Races.FindAsync(raceId);

            if (cyclist == null)
            {
                TempData["DatabaseError"] = "Renner niet gevonden.";
                return RedirectToAction(nameof(Index), new { search, status, page, pageSize });
            }

            if (race == null)
            {
                TempData["DatabaseError"] = "Race niet gevonden.";
                return RedirectToAction(nameof(Index), new { search, status, page, pageSize });
            }

            bool alreadyAdded = cyclist.RaceEntries.Any(re => re.RaceId == raceId);

            if (alreadyAdded)
            {
                TempData["Success"] = $"{cyclist.FullName} staat al in {race.Name}.";
                return RedirectToAction(nameof(Index), new { search, status, page, pageSize });
            }

            _context.RaceEntries.Add(new RaceEntry
            {
                RaceId = raceId,
                CyclistId = cyclistId,
                TeamId = cyclist.TeamId
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{cyclist.FullName} is toegevoegd aan {race.Name}.";
            return RedirectToAction(nameof(Index), new { search, status, page, pageSize });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cyclist = await _context.Cyclists.FindAsync(id);
            if (cyclist != null)
            {
                _context.Cyclists.Remove(cyclist);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{cyclist.FullName} is verwijderd.";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Index(string? search, string? status, int page = 1, int pageSize = 25)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = 1;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.CyclistCount = 0;
            ViewBag.Races = new List<Race>();
            ViewBag.Teams = new List<Team>();
            ViewBag.DatabaseOnline = false;

            try
            {
                var races = await _context.Races
                    .OrderByDescending(r => r.Year)
                    .ThenBy(r => r.Name)
                    .ToListAsync();

                var teams = await _context.Teams.OrderBy(t => t.Name).ToListAsync();

                var query = _context.Cyclists
                    .Include(c => c.Team)
                    .Include(c => c.RaceEntries)
                    .Where(c => c.PlayerSelections.Any())
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim();

                    query = query.Where(c =>
                        c.FirstName.Contains(search) ||
                        c.LastName.Contains(search) ||
                        (c.Team != null && c.Team.Name.Contains(search)));
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (status == "active")
                    {
                        query = query.Where(c => c.IsActive);
                    }
                    else if (status == "inactive")
                    {
                        query = query.Where(c => !c.IsActive);
                    }
                    else if (status.StartsWith("race-") && int.TryParse(status.Replace("race-", ""), out int raceId))
                    {
                        query = query.Where(c => c.RaceEntries.Any(re => re.RaceId == raceId));
                    }
                }

                var totalItems = await query.CountAsync();

                var cyclists = await query
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize));
                ViewBag.CyclistCount = totalItems;
                ViewBag.Races = races;
                ViewBag.Teams = teams;
                ViewBag.DatabaseOnline = true;

                return View(cyclists);
            }
            catch
            {
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om live gegevens te zien.";
                return View(new List<Cyclist>());
            }
        }
    }
}
