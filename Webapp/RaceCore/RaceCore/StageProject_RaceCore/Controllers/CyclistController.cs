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
        // ? INDEX
        public async Task<IActionResult> Index(string? search, string? status, int page = 1, int pageSize = 25)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : pageSize;

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.Status = status;

            try
            {
                var teams = await _context.Teams
                    .OrderBy(t => t.Name)
                    .ToListAsync();

                var query = _context.Cyclists
                    .Include(c => c.Team)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim();

                    query = query.Where(c =>
                        c.FirstName.Contains(search) ||
                        c.LastName.Contains(search) ||
                        (c.Team != null && c.Team.Name.Contains(search)));
                }

                if (status == "active")
                    query = query.Where(c => c.IsActive);
                else if (status == "inactive")
                    query = query.Where(c => !c.IsActive);

                var totalItems = await query.CountAsync();

                var cyclists = await query
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
                ViewBag.CyclistCount = totalItems;
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



        // ? GET: Create pagina (BELANGRIJK)
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Teams = await _context.Teams
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View();
        }

        // ? POST: Create cyclist
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string firstName, string lastName, int? teamId)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                TempData["CreateError"] = "Voornaam en achternaam zijn verplicht.";
                return RedirectToAction(nameof(Index));
            }

            var cyclist = new Cyclist
            {
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                TeamId = teamId, // null blijft null, geen 0 check nodig
                IsActive = true
            };

            _context.Cyclists.Add(cyclist);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{cyclist.FullName} is succesvol toegevoegd.";

            return RedirectToAction(nameof(Index));
        }

        // ? DELETE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cyclist = await _context.Cyclists.FindAsync(id);

            if (cyclist == null)
            {
                TempData["DatabaseError"] = "Cyclist niet gevonden.";
                return RedirectToAction(nameof(Index));
            }

            _context.Cyclists.Remove(cyclist);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{cyclist.FullName} is verwijderd.";

            return RedirectToAction(nameof(Index));
        }

        // ? TOGGLE ACTIVE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var cyclist = await _context.Cyclists.FindAsync(id);

            if (cyclist == null)
            {
                TempData["DatabaseError"] = "Cyclist niet gevonden.";
                return RedirectToAction(nameof(Index));
            }

            cyclist.IsActive = !cyclist.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] = cyclist.IsActive
                ? $"{cyclist.FullName} is actief gezet."
                : $"{cyclist.FullName} is inactief gezet.";

            return RedirectToAction(nameof(Index));
        }

    }
}