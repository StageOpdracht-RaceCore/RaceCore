using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
  public class RaceController : Controller
  {
    private readonly AppDbContext _context;

    // Inject the database context instead of using a static list
    public RaceController(AppDbContext context)
    {
      _context = context;
    }

    // READ: List all races
    public async Task<IActionResult> Index()
    {
      var races = await _context.Races.ToListAsync();
      return View(races);
    }

    // CREATE: Show the blank form
    public IActionResult Create()
    {
      return View();
    }

    // CREATE: Save the new race to the DB
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Year,StartDate,EndDate")] Race race)
    {
      if (ModelState.IsValid)
      {
        _context.Add(race);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
      }
      return View(race);
    }

    // UPDATE: Show the edit form with existing data
    public async Task<IActionResult> Edit(int? id)
    {
      if (id == null) return NotFound();

      var race = await _context.Races.FindAsync(id);
      if (race == null) return NotFound();

      return View(race);
    }

    // UPDATE: Save the changes
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Year,StartDate,EndDate")] Race race)
    {
      if (id != race.Id) return NotFound();

      if (ModelState.IsValid)
      {
        _context.Update(race);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
      }
      return View(race);
    }

    // DETAILS: Show details of a race
    public async Task<IActionResult> Details(int? id)
    {
      if (id == null) return NotFound();

      var race = await _context.Races
        .Include(r => r.Stages)
        .Include(r => r.RaceEntries)
        .FirstOrDefaultAsync(m => m.Id == id);

      if (race == null) return NotFound();

      return View(race);
    }

    // DELETE: Show confirmation
    public async Task<IActionResult> Delete(int? id)
    {
      if (id == null) return NotFound();

      var race = await _context.Races.FirstOrDefaultAsync(m => m.Id == id);
      if (race == null) return NotFound();

      return View(race);
    }

    // DELETE: Confirm and remove from DB
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
      var race = await _context.Races.FindAsync(id);
      if (race != null)
      {
        _context.Races.Remove(race);
        await _context.SaveChangesAsync();
      }
      return RedirectToAction(nameof(Index));
    }
  }
}
