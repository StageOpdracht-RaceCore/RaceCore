using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.Controllers
{
    /* TeamController.cs
       Purpose: Manages player teams during a game session. Responsible
       for listing players' teams, swapping active/bench riders and
       helper methods for building the view model. Business rules such
       as active/bench slots and color assignment live here.
    */
    /// <summary>
    /// Controller for team views and actions used during a game session.
    /// </summary>
    public class TeamController : Controller
    {
        private const int ActiveRiderSlots = 10;
        private const int BenchRiderSlots = 5;

        private static readonly string[] PlayerColors =
        {
            "#2563eb",
            "#16a34a",
            "#f59e0b",
            "#dc2626",
            "#7c3aed",
            "#0891b2",
            "#db2777",
            "#65a30d"
        };

        private readonly AppDbContext _context;

        public TeamController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int gameId = 0)
        {
            var model = new PlayerTeamsPageViewModel
            {
                ActiveRiderSlots = ActiveRiderSlots,
                BenchRiderSlots = BenchRiderSlots
            };

            try
            {
                if (!await _context.Database.CanConnectAsync())
                {
                    return View(model);
                }

                var game = await ResolveGame(gameId);

                if (game == null)
                {
                    return View(model);
                }

                model.GameId = game.Id;
                model.RaceId = game.RaceId;
                model.RaceName = $"{game.Race.Name} {game.Race.Year}";
                model.GameStatus = game.Status;

                var draftTurns = await _context.DraftTurns
                    .Include(d => d.Player)
                    .Where(d => d.GameSessionId == game.Id)
                    .OrderBy(d => d.TurnNumber)
                    .ToListAsync();

                var selections = await _context.PlayerSelections
                    .Include(s => s.Player)
                    .Include(s => s.Cyclist)
                        .ThenInclude(c => c.Team)
                    .Where(s => s.GameSessionId == game.Id)
                    .ToListAsync();

                var turnNumberByCyclistId = draftTurns
                    .Where(d => d.CyclistId.HasValue)
                    .GroupBy(d => d.CyclistId!.Value)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Min(d => d.TurnNumber));

                var players = draftTurns
                    .Where(d => d.Player != null)
                    .Select(d => d.Player)
                    .Concat(selections
                        .Where(s => s.Player != null)
                        .Select(s => s.Player))
                    .GroupBy(p => p.Id)
                    .Select(g => g.First())
                    .OrderBy(p => p.PositionInDraft)
                    .ThenBy(p => p.Name)
                    .ToList();

                for (var index = 0; index < players.Count; index++)
                {
                    var player = players[index];
                    var color = PlayerColors[index % PlayerColors.Length];
                    var playerSelections = selections
                        .Where(s => s.PlayerId == player.Id)
                        .ToList();

                    model.PlayerTeams.Add(new PlayerTeamViewModel
                    {
                        PlayerId = player.Id,
                        PlayerName = player.Name,
                        Initials = BuildInitials(player.Name),
                        PositionInDraft = player.PositionInDraft,
                        Color = color,
                        ColorSoft = ToRgba(color, 0.12),
                        ColorDark = Darken(color, 0.22),
                        TextColor = GetReadableTextColor(color),
                        ActiveRiders = BuildRiders(
                            playerSelections,
                            isActive: true,
                            turnNumberByCyclistId)
                            .Take(ActiveRiderSlots)
                            .ToList(),
                        BenchRiders = BuildRiders(
                            playerSelections,
                            isActive: false,
                            turnNumberByCyclistId)
                            .Take(BenchRiderSlots)
                            .ToList()
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SwapActiveBench(
            int gameId,
            int playerId,
            int activeCyclistId,
            int benchCyclistId)
        {
            try
            {
                var hasActive = activeCyclistId > 0;
                var hasBench = benchCyclistId > 0;

                if (!hasActive && !hasBench)
                {
                    return BadRequest();
                }

                PlayerSelection? activeSelection = null;
                PlayerSelection? benchSelection = null;

                if (hasActive)
                {
                    activeSelection = await _context.PlayerSelections
                        .FirstOrDefaultAsync(s =>
                            s.GameSessionId == gameId &&
                            s.PlayerId == playerId &&
                            s.CyclistId == activeCyclistId &&
                            s.IsActive == true);

                    if (activeSelection == null)
                    {
                        return NotFound();
                    }
                }

                if (hasBench)
                {
                    benchSelection = await _context.PlayerSelections
                        .FirstOrDefaultAsync(s =>
                            s.GameSessionId == gameId &&
                            s.PlayerId == playerId &&
                            s.CyclistId == benchCyclistId &&
                            s.IsActive == false);

                    if (benchSelection == null)
                    {
                        return NotFound();
                    }
                }

                // Swap (both present) or move into an empty slot (one side missing)
                if (hasActive && hasBench)
                {
                    // Active rider becomes bench, bench rider becomes active
                    activeSelection!.IsActive = false;
                    benchSelection!.IsActive = true;
                }
                else if (hasActive)
                {
                    // Move active rider to an empty bench slot
                    activeSelection!.IsActive = false;
                }
                else
                {
                    // Move bench rider to an empty active slot
                    benchSelection!.IsActive = true;
                }

                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500);
            }
        }

        private async Task<GameSession?> ResolveGame(int gameId)
        {
            var games = _context.GameSessions
                .Include(g => g.Race)
                .AsQueryable();

            if (gameId > 0)
            {
                return await games.FirstOrDefaultAsync(g => g.Id == gameId);
            }

            var currentGame = await games
                .Where(g => g.Status != "Finished")
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync();

            if (currentGame != null)
            {
                return currentGame;
            }

            return await games
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync();
        }

        private static List<PlayerTeamRiderViewModel> BuildRiders(
            IEnumerable<PlayerSelection> selections,
            bool isActive,
            IReadOnlyDictionary<int, int> turnNumberByCyclistId)
        {
            return selections
                .Where(s => s.IsActive == isActive && s.Cyclist != null)
                .OrderBy(s => turnNumberByCyclistId.TryGetValue(s.CyclistId, out var turnNumber)
                    ? turnNumber
                    : int.MaxValue)
                .ThenBy(s => s.Cyclist!.LastName)
                .ThenBy(s => s.Cyclist!.FirstName)
                .Select(s => new PlayerTeamRiderViewModel
                {
                    CyclistId = s.CyclistId,
                    FullName = s.Cyclist!.FullName,
                    ProTeamName = s.Cyclist.Team?.Name ?? "Geen ploeg",
                    PickNumber = turnNumberByCyclistId.TryGetValue(s.CyclistId, out var turnNumber)
                        ? turnNumber
                        : 0,
                    IsActive = isActive
                })
                .ToList();
        }

        private static string BuildInitials(string name)
        {
            var initials = name
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(part => part[0])
                .ToArray();

            return initials.Length == 0
                ? "?"
                : new string(initials).ToUpperInvariant();
        }

        private static string ToRgba(string color, double alpha)
        {
            if (!TryParseHexColor(color, out var red, out var green, out var blue))
            {
                return $"rgba(37, 99, 235, {alpha.ToString("0.##", CultureInfo.InvariantCulture)})";
            }

            return string.Create(
                CultureInfo.InvariantCulture,
                $"rgba({red}, {green}, {blue}, {alpha:0.##})");
        }

        private static string Darken(string color, double amount)
        {
            if (!TryParseHexColor(color, out var red, out var green, out var blue))
            {
                return "#1e40af";
            }

            var factor = 1 - amount;
            return $"#{(int)Math.Round(red * factor):X2}{(int)Math.Round(green * factor):X2}{(int)Math.Round(blue * factor):X2}";
        }

        private static string GetReadableTextColor(string color)
        {
            if (!TryParseHexColor(color, out var red, out var green, out var blue))
            {
                return "#ffffff";
            }

            var luminance = (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
            return luminance > 150 ? "#111827" : "#ffffff";
        }

        private static bool TryParseHexColor(
            string? color,
            out int red,
            out int green,
            out int blue)
        {
            red = 0;
            green = 0;
            blue = 0;

            if (string.IsNullOrWhiteSpace(color))
            {
                return false;
            }

            var value = color.Trim();

            if (value.StartsWith("#", StringComparison.Ordinal))
            {
                value = value[1..];
            }

            return value.Length == 6 &&
                   int.TryParse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out red) &&
                   int.TryParse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out green) &&
                   int.TryParse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out blue);
        }
    }
}
