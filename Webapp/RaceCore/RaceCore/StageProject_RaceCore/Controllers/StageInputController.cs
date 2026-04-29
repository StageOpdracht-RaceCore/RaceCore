using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class StageInputController : Controller
    {
        private readonly AppDbContext _context;

        public StageInputController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int gameId)
        {
            var game = await _context.GameSessions
                .Include(g => g.Race)
                .FirstOrDefaultAsync(g => g.Id == gameId);

            if (game == null)
            {
                TempData["Error"] = "Game niet gevonden.";
                return RedirectToAction("New", "Game");
            }

            if (game.Status == "Draft")
            {
                TempData["Error"] = "Rond eerst de draft af.";
                return RedirectToAction("Index", "Draft", new { gameId = game.Id });
            }

            var stage = await _context.Stages
                .Where(s => s.RaceId == game.RaceId && s.StageNumber == game.CurrentStageNumber + 1)
                .OrderBy(s => s.StageNumber)
                .FirstOrDefaultAsync();

            if (stage == null)
            {
                game.Status = "Finished";
                await _context.SaveChangesAsync();

                TempData["Success"] = "Alle ritten zijn verwerkt.";
                return RedirectToAction("Index", "Dashboard", new { gameId = game.Id });
            }

            var selectedCyclistIds = await _context.PlayerSelections
                .Where(s => s.GameSessionId == game.Id)
                .Select(s => s.CyclistId)
                .ToListAsync();

            var cyclists = await _context.Cyclists
                .Where(c => c.IsActive && selectedCyclistIds.Contains(c.Id))
                .OrderBy(c => c.FirstName)
                .ThenBy(c => c.LastName)
                .ToListAsync();

            var model = new StageInputViewModel
            {
                GameId = game.Id,
                StageId = stage.Id,
                RaceName = game.Race.Name + " " + game.Race.Year,
                StageName = "Rit " + stage.StageNumber + " - " + stage.Name
            };

            foreach (var cyclist in cyclists)
            {
                model.Cyclists.Add(new SelectListItem
                {
                    Value = cyclist.Id.ToString(),
                    Text = cyclist.FullName
                });
            }

            for (int i = 1; i <= 25; i++)
            {
                model.Rows.Add(new StageInputRow
                {
                    Position = i
                });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(StageInputViewModel model)
        {
            var game = await _context.GameSessions
                .FirstOrDefaultAsync(g => g.Id == model.GameId);

            if (game == null)
            {
                TempData["Error"] = "Game niet gevonden.";
                return RedirectToAction("New", "Game");
            }

            var stage = await _context.Stages
                .FirstOrDefaultAsync(s => s.Id == model.StageId);

            if (stage == null)
            {
                TempData["Error"] = "Rit niet gevonden.";
                return RedirectToAction("Index", "Dashboard", new { gameId = model.GameId });
            }

            var oldResults = await _context.StageResults
                .Where(r => r.StageId == model.StageId)
                .ToListAsync();

            var oldJerseys = await _context.Jerseys
                .Where(j => j.StageId == model.StageId)
                .ToListAsync();

            _context.StageResults.RemoveRange(oldResults);
            _context.Jerseys.RemoveRange(oldJerseys);

            var rows = model.Rows
                .Where(r => r.CyclistId != null)
                .ToList();

            foreach (var row in rows)
            {
                _context.StageResults.Add(new StageResult
                {
                    StageId = model.StageId,
                    CyclistId = row.CyclistId.Value,
                    Position = row.Position,
                    Status = "Finished"
                });
            }

            AddJersey(model.StageId, model.YellowJerseyCyclistId, "Yellow");
            AddJersey(model.StageId, model.GreenJerseyCyclistId, "Green");
            AddJersey(model.StageId, model.PolkaJerseyCyclistId, "Polka");
            AddJersey(model.StageId, model.WhiteJerseyCyclistId, "White");

            await _context.SaveChangesAsync();

            await CalculatePoints(game.Id, stage.Id);

            game.CurrentStageNumber = stage.StageNumber;
            game.Status = "Active";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Rit opgeslagen en punten berekend.";
            return RedirectToAction("Index", "Result", new { gameId = game.Id });
        }

        private void AddJersey(int stageId, int? cyclistId, string type)
        {
            if (cyclistId == null)
            {
                return;
            }

            _context.Jerseys.Add(new Jersey
            {
                StageId = stageId,
                CyclistId = cyclistId.Value,
                Type = type
            });
        }

        private async Task CalculatePoints(int gameId, int stageId)
        {
            var game = await _context.GameSessions
                .FirstAsync(g => g.Id == gameId);

            var oldPoints = await _context.PlayerPoints
                .Where(p => p.RaceId == game.RaceId && p.StageId == stageId)
                .ToListAsync();

            _context.PlayerPoints.RemoveRange(oldPoints);

            var results = await _context.StageResults
                .Where(r => r.StageId == stageId && r.Position != null)
                .ToListAsync();

            var jerseys = await _context.Jerseys
                .Where(j => j.StageId == stageId)
                .ToListAsync();

            var rules = await _context.PointsRules.ToListAsync();

            var selections = await _context.PlayerSelections
                .Where(s => s.GameSessionId == gameId && s.IsActive)
                .ToListAsync();

            foreach (var selection in selections)
            {
                int totalPoints = 0;

                var result = results.FirstOrDefault(r => r.CyclistId == selection.CyclistId);

                if (result != null)
                {
                    var rule = rules.FirstOrDefault(r =>
                        r.Type == "Rit" &&
                        r.FromPosition <= result.Position &&
                        r.ToPosition >= result.Position);

                    if (rule != null)
                    {
                        totalPoints += rule.Points;
                    }
                }

                foreach (var jersey in jerseys)
                {
                    if (jersey.CyclistId == selection.CyclistId)
                    {
                        string type = GetPointsRuleType(jersey.Type);

                        var jerseyPoints = rules
                            .Where(r => r.Type == type)
                            .Sum(r => r.Points);

                        totalPoints += jerseyPoints;
                    }
                }

                if (totalPoints > 0)
                {
                    _context.PlayerPoints.Add(new PlayerPoints
                    {
                        PlayerId = selection.PlayerId,
                        RaceId = game.RaceId,
                        StageId = stageId,
                        CyclistId = selection.CyclistId,
                        Points = totalPoints
                    });
                }
            }
        }

        private string GetPointsRuleType(string jerseyType)
        {
            if (jerseyType == "Yellow")
            {
                return "RodeTrui";
            }

            if (jerseyType == "Green")
            {
                return "GroeneTrui";
            }

            if (jerseyType == "Polka")
            {
                return "BlauweTrui";
            }

            if (jerseyType == "White")
            {
                return "WitteTrui";
            }

            return jerseyType;
        }
    }
}