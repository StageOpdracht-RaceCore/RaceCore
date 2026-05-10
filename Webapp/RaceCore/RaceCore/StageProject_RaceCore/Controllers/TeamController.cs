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
    public class TeamController : Controller
    {
        // Maximum number of cyclists a player can field in the starting lineup.
        private const int ActiveRiderSlots = 10;

        // Maximum number of cyclists a player can keep on the bench.
        private const int BenchRiderSlots = 5;

        // Colour palette cycled across player cards in display order.
        // Chosen for contrast and visual distinctiveness against white card backgrounds.
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

        // GET /Team or GET /Team?gameId=X
        // Loads the Teams overview page. Resolves the target game session (either the one
        // explicitly requested via gameId, the most recent non-finished game, or the latest
        // game overall), then builds a PlayerTeamsPageViewModel containing every player's
        // full roster split into active riders and bench riders.
        public async Task<IActionResult> Index(int gameId = 0)
        {
            // Start with an empty view model so the page renders gracefully if anything fails.
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

                // Load every draft turn for this game session, ordered chronologically.
                // DraftTurns record which player picked which cyclist on which turn.
                var draftTurns = await _context.DraftTurns
                    .Include(d => d.Player)
                    .Where(d => d.GameSessionId == game.Id)
                    .OrderBy(d => d.TurnNumber)
                    .ToListAsync();

                // Load every player selection for this game, including the cyclist and their pro team.
                var selections = await _context.PlayerSelections
                    .Include(s => s.Player)
                    .Include(s => s.Cyclist)
                        .ThenInclude(c => c.Team)
                    .Where(s => s.GameSessionId == game.Id)
                    .ToListAsync();

                // Build a lookup of cyclist → earliest draft turn number so riders can be
                // displayed in pick order rather than insertion order.
                var turnNumberByCyclistId = draftTurns
                    .Where(d => d.CyclistId.HasValue)
                    .GroupBy(d => d.CyclistId!.Value)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Min(d => d.TurnNumber));

                // Collect the distinct set of players from both draft turns and selections,
                // then order by their draft position so cards appear in a consistent order.
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

                // Build one PlayerTeamViewModel per player, assigning a colour from the
                // palette and splitting their selections into active and bench lists.
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

        // POST /Team/SwapActiveBench
        // Moves a cyclist between the active lineup and the bench for a specific player in a game.
        // Accepts up to two cyclist IDs:
        //   activeCyclistId — the cyclist currently active who should move to the bench (0 = empty slot).
        //   benchCyclistId  — the cyclist currently on the bench who should become active (0 = empty slot).
        // At least one must be non-zero; if only one is provided the other slot is treated as empty.
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

                // Reject requests where both IDs are zero — nothing to swap.
                if (!hasActive && !hasBench)
                {
                    return BadRequest();
                }

                PlayerSelection? activeSelection = null;
                PlayerSelection? benchSelection = null;

                // Look up the active cyclist's selection record, verifying it is currently active.
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

                // Look up the bench cyclist's selection record, verifying it is currently inactive.
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

                if (hasActive && hasBench)
                {
                    // Full swap: active rider moves to bench, bench rider becomes active.
                    activeSelection!.IsActive = false;
                    benchSelection!.IsActive = true;
                }
                else if (hasActive)
                {
                    // One-sided: move an active rider into an empty bench slot.
                    activeSelection!.IsActive = false;
                }
                else
                {
                    // One-sided: move a bench rider into an empty active slot.
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

        // Resolves which game session to display.
        // Priority: explicit gameId → most recent non-finished game → most recent game overall.
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

        // Filters a player's selections to either active or bench riders, sorts them by
        // draft turn number (i.e. pick order), and projects them into view model rows.
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

        // Produces a two-letter initials string from the first two words of a name.
        // e.g. "Wout van Aert" → "WV". Falls back to "?" for empty names.
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

        // Converts a CSS hex colour to an rgba() string with the given alpha value.
        // Used to produce the soft background tint for active-rider rows.
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

        // Darkens a hex colour by the given amount (0–1) by scaling each RGB channel down.
        // Used to produce gradient end colours and hover shades for player cards.
        private static string Darken(string color, double amount)
        {
            if (!TryParseHexColor(color, out var red, out var green, out var blue))
            {
                return "#1e40af";
            }

            var factor = 1 - amount;
            return $"#{(int)Math.Round(red * factor):X2}{(int)Math.Round(green * factor):X2}{(int)Math.Round(blue * factor):X2}";
        }

        // Returns "#ffffff" (white) or "#111827" (near-black) depending on the perceived
        // luminance of the background colour, so text remains legible at all times.
        private static string GetReadableTextColor(string color)
        {
            if (!TryParseHexColor(color, out var red, out var green, out var blue))
            {
                return "#ffffff";
            }

            var luminance = (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
            return luminance > 150 ? "#111827" : "#ffffff";
        }

        // Parses a 6-digit hex colour string (with or without leading #) into R, G, B components.
        // Returns false and zeroes if the string is null, empty, or not a valid hex colour.
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
