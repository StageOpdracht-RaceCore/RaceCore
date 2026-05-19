using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    public class CyclistController : Controller
    {
        // ============================================================
        // DATABASE CONTEXT
        // ============================================================

        private readonly AppDbContext _context;

        public CyclistController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // INDEX - TOONT ALLE WIELRENNERS MET FILTERS EN PAGINATIE
        // ============================================================

        public async Task<IActionResult> Index(string? search, string? status, int page = 1, int pageSize = 25)
        {
            // Zorgt dat pagina en pageSize altijd geldig zijn
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 25;

            // Standaard ViewBag waarden
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
                // Alle races ophalen voor dropdown/filter
                var races = await _context.Races
                    .OrderByDescending(r => r.Year)
                    .ThenBy(r => r.Name)
                    .ToListAsync();

                // Alle teams ophalen voor dropdown bij toevoegen
                var teams = await _context.Teams
                    .OrderBy(t => t.Name)
                    .ToListAsync();

                // Basisquery voor wielrenners
                var query = _context.Cyclists
                    .Include(c => c.Team)
                    .Include(c => c.RaceEntries)
                    .AsQueryable();

                // Zoeken op voornaam, achternaam of teamnaam
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim();

                    query = query.Where(c =>
                        c.FirstName.Contains(search) ||
                        c.LastName.Contains(search) ||
                        (c.Team != null && c.Team.Name.Contains(search)));
                }

                // Filteren op status of race
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
                    else if (status.StartsWith("race-") &&
                             int.TryParse(status.Replace("race-", ""), out int raceId))
                    {
                        query = query.Where(c =>
                            c.RaceEntries.Any(re => re.RaceId == raceId));
                    }
                }

                // Totaal aantal renners na filters
                var totalItems = await query.CountAsync();

                // Renners ophalen met sortering en paginatie
                var cyclists = await query
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // ViewBag waarden voor de View
                ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize));
                ViewBag.CyclistCount = totalItems;
                ViewBag.Races = races;
                ViewBag.Teams = teams;
                ViewBag.DatabaseOnline = true;

                return View(cyclists);
            }
            catch
            {
                // Foutmelding als database niet bereikbaar is
                TempData["DatabaseError"] = "Database unavailable. Start OpenVPN to view live data.";

                return View(new List<Cyclist>());
            }
        }

        // ============================================================
        // CREATE - NIEUWE WIELRENNER TOEVOEGEN
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Teams = await _context.Teams
                .OrderBy(t => t.Name)
                .ToListAsync();

            ViewBag.Races = await _context.Races
                .OrderByDescending(r => r.Year)
                .ThenBy(r => r.Name)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string firstName,
            string lastName,
            int? teamId,
            bool isActive = true,
            int? raceId = null)
        {
            // Controle of voornaam en achternaam ingevuld zijn
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName))
            {
                TempData["CreateError"] = "First name and last name are required.";
                return RedirectToAction(nameof(Index));
            }

            Race? selectedRace = null;

            // Controle of geselecteerde race bestaat
            if (raceId.HasValue && raceId.Value > 0)
            {
                selectedRace = await _context.Races.FindAsync(raceId.Value);

                if (selectedRace == null)
                {
                    TempData["CreateError"] = "Selected race does not exist.";
                    return RedirectToAction(nameof(Index));
                }
            }

            // Nieuwe wielrenner aanmaken
            var cyclist = new Cyclist
            {
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                TeamId = teamId == 0 ? null : teamId,
                IsActive = isActive
            };

            _context.Cyclists.Add(cyclist);
            await _context.SaveChangesAsync();

            // Als er een race gekozen is, koppel de renner direct aan die race
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

            TempData["Success"] = $"{cyclist.FullName} was added successfully.";

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // ADD TO RACE - BESTAANDE WIELRENNER TOEVOEGEN AAN RACE
        // ============================================================

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
            // Renner ophalen inclusief bestaande race-koppelingen
            var cyclist = await _context.Cyclists
                .Include(c => c.RaceEntries)
                .FirstOrDefaultAsync(c => c.Id == cyclistId);

            // Race ophalen
            var race = await _context.Races.FindAsync(raceId);

            // Controle of renner bestaat
            if (cyclist == null)
            {
                TempData["DatabaseError"] = "Cyclist not found.";

                return RedirectToAction(nameof(Index), new { search, status, page, pageSize });
            }

            // Controle of race bestaat
            if (race == null)
            {
                TempData["DatabaseError"] = "Race not found.";

                return RedirectToAction(nameof(Index), new { search, status, page, pageSize });
            }

            // Controle of renner al aan deze race gekoppeld is
            bool alreadyAdded = cyclist.RaceEntries.Any(re => re.RaceId == raceId);

            if (alreadyAdded)
            {
                TempData["Success"] = $"{cyclist.FullName} is already in {race.Name}.";

                return RedirectToAction(nameof(Index), new { search, status, page, pageSize });
            }

            // Nieuwe race-koppeling maken
            _context.RaceEntries.Add(new RaceEntry
            {
                RaceId = raceId,
                CyclistId = cyclistId,
                TeamId = cyclist.TeamId
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{cyclist.FullName} was added to {race.Name}.";

            return RedirectToAction(nameof(Index), new { search, status, page, pageSize });
        }

        // ============================================================
        // TOGGLE ACTIVE - WIELRENNER ACTIEF/INACTIEF ZETTEN
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            // Renner zoeken
            var cyclist = await _context.Cyclists.FindAsync(id);

            if (cyclist == null)
            {
                TempData["DatabaseError"] = "Cyclist not found.";
                return RedirectToAction(nameof(Index));
            }

            // Status omkeren
            cyclist.IsActive = !cyclist.IsActive;

            await _context.SaveChangesAsync();

            // Melding tonen
            TempData["Success"] = cyclist.IsActive
                ? $"{cyclist.FullName} was set to active."
                : $"{cyclist.FullName} was set to inactive.";

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // DELETE - WIELRENNER VERWIJDEREN
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            // Renner zoeken
            var cyclist = await _context.Cyclists.FindAsync(id);

            if (cyclist != null)
            {
                _context.Cyclists.Remove(cyclist);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"{cyclist.FullName} was deleted.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}