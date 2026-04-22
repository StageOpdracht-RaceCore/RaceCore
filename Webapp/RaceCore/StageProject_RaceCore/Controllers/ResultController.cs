using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using System.Text.RegularExpressions;

namespace StageProject_RaceCore.Controllers
{
  public class ResultVM
  {
    public string StageName { get; set; } = "";
    public string CyclistName { get; set; } = "";
    public int points { get; set; }
    public string JerseyType { get; set; } = "";
    public int totalPoints { get; set; }

  }

    public class ResultController : Controller
    {

    private readonly AppDbContext _context;

    public ResultController(AppDbContext appDbContext)
    {
      _context = appDbContext;
    }

        public async Task<IActionResult>index()
      {
      var rankData = await _context.PlayerPoints
        .Include(pp => pp.Player)
        .GroupBy(pp => pp.Player.Name)
        .Select(group => new ResultVM
        {
           CyclistName = group.Key,
          totalPoints = group.Sum(pp => pp.Points),
           JerseyType = "Leader"
        })
         .OrderByDescending(r => r.totalPoints).ToListAsync();

      return View(rankData);

    }

    }


}
