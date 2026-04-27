using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

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

        public async Task<IActionResult> Index()
        {
            try
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
                    .OrderByDescending(r => r.totalPoints)
                    .ToListAsync();

                ViewBag.DatabaseOnline = true;
                return View(rankData);
            }
            catch
            {
                ViewBag.DatabaseOnline = false;
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om live resultaten te zien.";
                return View(new List<ResultVM>());
            }
        }

        public async Task<IActionResult> StageResults(int stageId)
        {
            try
            {
                var stageResults = await _context.PlayerPoints
                    .Where(pp => pp.StageId == stageId)
                    .Include(pp => pp.Player)
                    .Include(pp => pp.Stage)
                    .Select(pp => new ResultVM
                    {
                        CyclistName = pp.Player.Name,
                        points = pp.Points,
                        StageName = pp.Stage != null ? pp.Stage.Name : "",
                        JerseyType = pp.Reason ?? "Stage"
                    })
                    .OrderByDescending(r => r.points)
                    .ToListAsync();

                ViewBag.DatabaseOnline = true;
                return View(stageResults);
            }
            catch
            {
                ViewBag.DatabaseOnline = false;
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om ritresultaten te zien.";
                return View(new List<ResultVM>());
            }
        }

        public async Task<IActionResult> ManageStageInput(int? raceId, int? stageId)
        {
            var races = new List<Race>();
            var stages = new List<Stage>();
            var cyclists = new List<Cyclist>();
            var stageResults = new List<StageResult>();
            var jerseys = new List<Jersey>();

            try
            {
                races = await _context.Races
                    .OrderByDescending(r => r.Year)
                    .ThenBy(r => r.Name)
                    .ToListAsync();

                if (!raceId.HasValue && races.Any())
                    raceId = races.First().Id;

                if (raceId.HasValue)
                {
                    stages = await _context.Stages
                        .Where(s => s.RaceId == raceId.Value)
                        .OrderBy(s => s.StageNumber)
                        .ToListAsync();
                }

                if (!stageId.HasValue && stages.Any())
                    stageId = stages.First().Id;

                if (raceId.HasValue)
                {
                    cyclists = await _context.RaceEntries
                        .Where(re => re.RaceId == raceId.Value)
                        .Select(re => re.CyclistId)
                        .Distinct()
                        .Join(
                            _context.Cyclists,
                            cyclistId => cyclistId,
                            cyclist => cyclist.Id,
                            (cyclistId, cyclist) => cyclist
                        )
                        .OrderBy(c => c.LastName)
                        .ThenBy(c => c.FirstName)
                        .ToListAsync();
                }

                if (stageId.HasValue)
                {
                    stageResults = await _context.StageResults
                        .Where(sr => sr.StageId == stageId.Value)
                        .OrderBy(sr => sr.Position)
                        .ToListAsync();

                    jerseys = await _context.Jerseys
                        .Where(j => j.StageId == stageId.Value)
                        .ToListAsync();
                }

                ViewBag.DatabaseOnline = true;
            }
            catch
            {
                ViewBag.DatabaseOnline = false;
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om ritresultaten in te voeren.";
            }

            ViewBag.Races = races;
            ViewBag.Stages = stages;
            ViewBag.Cyclists = cyclists;
            ViewBag.SelectedRaceId = raceId;
            ViewBag.SelectedStageId = stageId;
            ViewBag.StageResults = stageResults;
            ViewBag.Jerseys = jerseys;

            ViewBag.RodeTruiCyclistId = jerseys.FirstOrDefault(j =>
                string.Equals(j.Type, "RodeTrui", StringComparison.OrdinalIgnoreCase))?.CyclistId;

            ViewBag.GroeneTruiCyclistId = jerseys.FirstOrDefault(j =>
                string.Equals(j.Type, "GroeneTrui", StringComparison.OrdinalIgnoreCase))?.CyclistId;

            ViewBag.BlauweTruiCyclistId = jerseys.FirstOrDefault(j =>
                string.Equals(j.Type, "BlauweTrui", StringComparison.OrdinalIgnoreCase))?.CyclistId;

            ViewBag.WitteTruiCyclistId = jerseys.FirstOrDefault(j =>
                string.Equals(j.Type, "WitteTrui", StringComparison.OrdinalIgnoreCase))?.CyclistId;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTop25(
            int selectedRaceId,
            int selectedStageId,
            List<int?> cyclistIds,
            List<string?> jerseys,
            int? extraRodeTruiCyclistId,
            int? extraGroeneTruiCyclistId,
            int? extraBlauweTruiCyclistId,
            int? extraWitteTruiCyclistId)
        {
            try
            {
                var existingResults = await _context.StageResults
                    .Where(sr => sr.StageId == selectedStageId)
                    .ToListAsync();

                var existingJerseys = await _context.Jerseys
                    .Where(j => j.StageId == selectedStageId)
                    .ToListAsync();

                _context.StageResults.RemoveRange(existingResults);
                _context.Jerseys.RemoveRange(existingJerseys);

                cyclistIds ??= new List<int?>();
                jerseys ??= new List<string?>();

                for (int i = 0; i < 25; i++)
                {
                    if (cyclistIds.Count > i && cyclistIds[i].HasValue)
                    {
                        var cyclistId = cyclistIds[i]!.Value;

                        _context.StageResults.Add(new StageResult
                        {
                            StageId = selectedStageId,
                            CyclistId = cyclistId,
                            Position = i + 1
                        });

                        if (jerseys.Count > i && !string.IsNullOrWhiteSpace(jerseys[i]))
                        {
                            _context.Jerseys.Add(new Jersey
                            {
                                StageId = selectedStageId,
                                CyclistId = cyclistId,
                                Type = jerseys[i]!.Trim()
                            });
                        }
                    }
                }

                void AddExtraJersey(int? cyclistId, string type)
                {
                    if (!cyclistId.HasValue) return;

                    var alreadyAdded = _context.Jerseys.Local.Any(j =>
                        j.StageId == selectedStageId &&
                        j.CyclistId == cyclistId.Value &&
                        string.Equals(j.Type, type, StringComparison.OrdinalIgnoreCase));

                    if (!alreadyAdded)
                    {
                        _context.Jerseys.Add(new Jersey
                        {
                            StageId = selectedStageId,
                            CyclistId = cyclistId.Value,
                            Type = type
                        });
                    }
                }

                AddExtraJersey(extraRodeTruiCyclistId, "RodeTrui");
                AddExtraJersey(extraGroeneTruiCyclistId, "GroeneTrui");
                AddExtraJersey(extraBlauweTruiCyclistId, "BlauweTrui");
                AddExtraJersey(extraWitteTruiCyclistId, "WitteTrui");

                await _context.SaveChangesAsync();
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
            }

            return RedirectToAction(nameof(ManageStageInput), new
            {
                raceId = selectedRaceId,
                stageId = selectedStageId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveJerseys(
            int selectedRaceId,
            int selectedStageId,
            int? rodeTruiCyclistId,
            int? groeneTruiCyclistId,
            int? blauweTruiCyclistId,
            int? witteTruiCyclistId)
        {
            try
            {
                var existingJerseys = await _context.Jerseys
                    .Where(j => j.StageId == selectedStageId)
                    .ToListAsync();

                _context.Jerseys.RemoveRange(existingJerseys);

                void AddJersey(int? cyclistId, string type)
                {
                    if (cyclistId.HasValue)
                    {
                        _context.Jerseys.Add(new Jersey
                        {
                            StageId = selectedStageId,
                            CyclistId = cyclistId.Value,
                            Type = type
                        });
                    }
                }

                AddJersey(rodeTruiCyclistId, "RodeTrui");
                AddJersey(groeneTruiCyclistId, "GroeneTrui");
                AddJersey(blauweTruiCyclistId, "BlauweTrui");
                AddJersey(witteTruiCyclistId, "WitteTrui");

                await _context.SaveChangesAsync();
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
            }

            return RedirectToAction(nameof(ManageStageInput), new
            {
                raceId = selectedRaceId,
                stageId = selectedStageId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalculateBonusPoints(int selectedRaceId, int selectedStageId)
        {
            try
            {
                var stage = await _context.Stages
                    .FirstOrDefaultAsync(s => s.Id == selectedStageId);

                if (stage == null)
                {
                    return RedirectToAction(nameof(ManageStageInput), new
                    {
                        raceId = selectedRaceId,
                        stageId = selectedStageId
                    });
                }

                var results = await _context.StageResults
                    .Where(sr => sr.StageId == selectedStageId)
                    .OrderBy(sr => sr.Position)
                    .ToListAsync();

                var jerseys = await _context.Jerseys
                    .Where(j => j.StageId == selectedStageId)
                    .ToListAsync();

                var selections = await _context.PlayerSelections
                    .Where(ps => ps.RaceId == stage.RaceId)
                    .ToListAsync();

                var pointsRules = await _context.PointsRules.ToListAsync();

                var oldPoints = await _context.PlayerPoints
                    .Where(pp => pp.StageId == selectedStageId)
                    .ToListAsync();

                _context.PlayerPoints.RemoveRange(oldPoints);

                foreach (var result in results)
                {
                    if (!result.Position.HasValue) continue;

                    var rule = pointsRules.FirstOrDefault(pr =>
                        string.Equals(pr.Type, "Rit", StringComparison.OrdinalIgnoreCase) &&
                        pr.FromPosition.HasValue &&
                        pr.ToPosition.HasValue &&
                        result.Position.Value >= pr.FromPosition.Value &&
                        result.Position.Value <= pr.ToPosition.Value);

                    if (rule == null) continue;

                    var owners = selections.Where(s => s.CyclistId == result.CyclistId).ToList();

                    foreach (var owner in owners)
                    {
                        _context.PlayerPoints.Add(new PlayerPoints
                        {
                            PlayerId = owner.PlayerId,
                            RaceId = owner.RaceId,
                            StageId = selectedStageId,
                            CyclistId = owner.CyclistId,
                            Points = rule.Points,
                            Reason = $"Rit plaats {result.Position}"
                        });
                    }
                }

                foreach (var jersey in jerseys)
                {
                    var rule = pointsRules.FirstOrDefault(pr =>
                        !string.IsNullOrWhiteSpace(pr.Type) &&
                        !string.IsNullOrWhiteSpace(jersey.Type) &&
                        string.Equals(pr.Type.Trim(), jersey.Type.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (rule == null) continue;

                    var owners = selections.Where(s => s.CyclistId == jersey.CyclistId).ToList();

                    foreach (var owner in owners)
                    {
                        _context.PlayerPoints.Add(new PlayerPoints
                        {
                            PlayerId = owner.PlayerId,
                            RaceId = owner.RaceId,
                            StageId = selectedStageId,
                            CyclistId = owner.CyclistId,
                            Points = rule.Points,
                            Reason = jersey.Type
                        });
                    }
                }

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(StageResults), new { stageId = selectedStageId });
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
                return RedirectToAction(nameof(ManageStageInput), new
                {
                    raceId = selectedRaceId,
                    stageId = selectedStageId
                });
            }
        }
    }
}
