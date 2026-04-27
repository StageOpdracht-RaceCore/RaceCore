using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class GameController : Controller
    {
        private readonly AppDbContext _context;

        public GameController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> New()
        {
            var model = await BuildNewGameViewModelSafe();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(NewGameViewModel model)
        {
            model.SelectedPlayerIds = model.SelectedPlayerIds
                .Distinct()
                .ToList();

            if (model.RaceId <= 0)
            {
                ModelState.AddModelError(nameof(model.RaceId), "Kies een race.");
            }

            if (model.SelectedPlayerIds.Count < 2)
            {
                ModelState.AddModelError(nameof(model.SelectedPlayerIds), "Kies minstens 2 spelers.");
            }

            if (!ModelState.IsValid)
            {
                return View(await BuildNewGameViewModelSafe(model.RaceId, model.SelectedPlayerIds));
            }

            try
            {
                var race = await _context.Races
                    .FirstOrDefaultAsync(r => r.Id == model.RaceId);

                if (race == null)
                {
                    TempData["Error"] = "Race niet gevonden.";
                    return View(await BuildNewGameViewModelSafe(model.RaceId, model.SelectedPlayerIds));
                }

                var players = await _context.Players
                    .Where(p => model.SelectedPlayerIds.Contains(p.Id))
                    .OrderBy(p => p.PositionInDraft)
                    .ThenBy(p => p.Id)
                    .ToListAsync();

                if (players.Count < 2)
                {
                    TempData["Error"] = "Kies minstens 2 geldige spelers.";
                    return View(await BuildNewGameViewModelSafe(model.RaceId, model.SelectedPlayerIds));
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                var stageIds = await _context.Stages
                    .Where(s => s.RaceId == model.RaceId)
                    .Select(s => s.Id)
                    .ToListAsync();

                var oldDraftTurns = await _context.DraftTurns
                    .Where(d => d.RaceId == model.RaceId)
                    .ToListAsync();

                var oldSelections = await _context.PlayerSelections
                    .Where(ps => ps.RaceId == model.RaceId)
                    .ToListAsync();

                var oldPlayerPoints = await _context.PlayerPoints
                    .Where(pp => pp.RaceId == model.RaceId)
                    .ToListAsync();

                var oldStageResults = await _context.StageResults
                    .Where(sr => stageIds.Contains(sr.StageId))
                    .ToListAsync();

                var oldJerseys = await _context.Jerseys
                    .Where(j => stageIds.Contains(j.StageId))
                    .ToListAsync();

                _context.DraftTurns.RemoveRange(oldDraftTurns);
                _context.PlayerSelections.RemoveRange(oldSelections);
                _context.PlayerPoints.RemoveRange(oldPlayerPoints);
                _context.StageResults.RemoveRange(oldStageResults);
                _context.Jerseys.RemoveRange(oldJerseys);

                foreach (var player in players)
                {
                    player.TotalPoints = 0;
                }

                await _context.SaveChangesAsync();

                const int totalRounds = 15;
                var draftTurns = GenerateFairSnakeDraft(model.RaceId, players, totalRounds);

                _context.DraftTurns.AddRange(draftTurns);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["Success"] = $"Nieuwe game gestart voor {race.Name} {race.Year}.";
                return RedirectToAction("Index", "Draft", new { raceId = model.RaceId });
            }
            catch
            {
                TempData["Error"] = "Database niet bereikbaar. Start OpenVPN en probeer opnieuw.";
                return View(await BuildNewGameViewModelSafe(model.RaceId, model.SelectedPlayerIds));
            }
        }

        private async Task<NewGameViewModel> BuildNewGameViewModelSafe(int selectedRaceId = 0, List<int>? selectedPlayerIds = null)
        {
            try
            {
                return await BuildNewGameViewModel(selectedRaceId, selectedPlayerIds);
            }
            catch
            {
                TempData["DatabaseError"] = "Database niet bereikbaar. Start OpenVPN om races en spelers te laden.";

                return new NewGameViewModel
                {
                    RaceId = selectedRaceId,
                    SelectedPlayerIds = selectedPlayerIds ?? new List<int>(),
                    AvailableRaces = new List<SelectListItem>(),
                    AvailablePlayers = new List<PlayerSelectItemViewModel>(),
                    TotalStages = 0,
                    TotalCyclists = 0
                };
            }
        }

        private async Task<NewGameViewModel> BuildNewGameViewModel(int selectedRaceId = 0, List<int>? selectedPlayerIds = null)
        {
            selectedPlayerIds ??= new List<int>();

            var races = await _context.Races
                .Include(r => r.Stages)
                .OrderByDescending(r => r.Year)
                .ThenBy(r => r.Name)
                .ToListAsync();

            var players = await _context.Players
                .OrderBy(p => p.PositionInDraft)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var selectedRace = selectedRaceId > 0
                ? races.FirstOrDefault(r => r.Id == selectedRaceId)
                : races.FirstOrDefault();

            int raceId = selectedRace?.Id ?? 0;

            if (!selectedPlayerIds.Any())
            {
                selectedPlayerIds = players.Select(p => p.Id).ToList();
            }

            int totalCyclists = await _context.Cyclists
                .CountAsync(c => c.IsActive);

            return new NewGameViewModel
            {
                RaceId = raceId,
                SelectedPlayerIds = selectedPlayerIds,
                TotalStages = selectedRace?.Stages.Count ?? 0,
                TotalCyclists = totalCyclists,

                AvailableRaces = races.Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = $"{r.Name} {r.Year} ({r.Stages.Count} ritten)",
                    Selected = r.Id == raceId
                }).ToList(),

                AvailablePlayers = players.Select(p => new PlayerSelectItemViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    PositionInDraft = p.PositionInDraft,
                    IsSelected = selectedPlayerIds.Contains(p.Id)
                }).ToList()
            };
        }

        private static List<DraftTurn> GenerateFairSnakeDraft(int raceId, List<Player> players, int totalRounds)
        {
            var draftTurns = new List<DraftTurn>();

            int turnNumber = 1;
            int playerCount = players.Count;

            for (int round = 1; round <= totalRounds; round++)
            {
                int pairIndex = (round - 1) / 2;
                int startIndex = pairIndex % playerCount;

                var roundPlayers = players
                    .Skip(startIndex)
                    .Concat(players.Take(startIndex))
                    .ToList();

                if (round % 2 == 0)
                {
                    roundPlayers.Reverse();
                }

                foreach (var player in roundPlayers)
                {
                    draftTurns.Add(new DraftTurn
                    {
                        RaceId = raceId,
                        PlayerId = player.Id,
                        TurnNumber = turnNumber
                    });

                    turnNumber++;
                }
            }

            return draftTurns;
        }
    }
}