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
       Purpose: Manages teams during a game session. Responsible
       for listing players' teams, swapping active/benched riders and
       helper methods for building the view model. Business rules such
       as active/bench slots and color assignment live here.
    */
    // Controller for team views and actions used during a game session.
    public class TeamController : Controller
    {
        // Number of rider slots shown as active for each player.
        private const int ActiveRiderSlots = 10;
        // Number of rider slots shown on the bench for each player.
        private const int BenchRiderSlots = 5;

        // Reusable player color palette used to make each team visually distinct.
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

        // Creates a controller instance with the application database context.
        // context: Database context used to load game, player, and rider data.
        public TeamController(AppDbContext context)
        {
            _context = context;
        }

        // Builds the team overview for the selected game, or the latest available game when none is specified.
        // gameId: Optional game session identifier.
        // Returns the team overview page.
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

                // Return the empty page model when no game session can be resolved.
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

                // Store each selected rider's earliest draft turn so teams can be displayed in draft order.
                var turnNumberByCyclistId = draftTurns
                    .Where(d => d.CyclistId.HasValue)
                    .GroupBy(d => d.CyclistId!.Value)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Min(d => d.TurnNumber));

                // Combine players from draft turns and saved selections to avoid dropping teams with partial data.
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

        // Swaps riders between active and bench slots, or moves a rider into an empty opposite slot.
        // gameId: Game session containing the selections.
        // playerId: Player whose team is being updated.
        // activeCyclistId: Cyclist currently in an active slot, or zero when the active slot is empty.
        // benchCyclistId: Cyclist currently on the bench, or zero when the bench slot is empty.
        // Returns an HTTP result describing whether the update succeeded.
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
                if (!await _context.Database.CanConnectAsync())
                {
                    return BadRequest();
                }

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
                            s.CyclistId == activeCyclistId);

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
                            s.CyclistId == benchCyclistId);

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

        // Resolves the requested game session, falling back to the newest unfinished game and then the newest game overall.
        // gameId: Requested game session identifier, or zero to use the fallback order.
        // Returns the resolved game session, or null when no sessions exist.
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

        // Converts saved player selections into rider view models for either active or bench slots.
        // selections: Selections belonging to a single player.
        // isActive: Whether to build active riders or bench riders.
        // turnNumberByCyclistId: Draft turn lookup used for sorting and display.
        // Returns rider view models sorted by draft order and rider name.
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

        // Builds a short uppercase initials label from a player's name.
        // name: Player display name.
        // Returns one or two initials, or a question mark when the name is empty.
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

        // Converts a hex color into an rgba color string with the provided alpha value.
        // color: Hex color value.
        // alpha: Alpha channel value between transparent and opaque.
        // Returns an rgba color string, or a blue fallback when parsing fails.
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

        // Darkens a hex color by the provided amount.
        // color: Hex color value.
        // amount: Fraction used to reduce each color channel.
        // Returns a darkened hex color, or a blue fallback when parsing fails.
        private static string Darken(string color, double amount)
        {
            if (!TryParseHexColor(color, out var red, out var green, out var blue))
            {
                return "#1e40af";
            }

            var factor = 1 - amount;
            return $"#{(int)Math.Round(red * factor):X2}{(int)Math.Round(green * factor):X2}{(int)Math.Round(blue * factor):X2}";
        }

        // Selects a readable foreground color for the provided background color.
        // color: Hex background color value.
        // Returns a dark or light text color with better contrast against the background.
        private static string GetReadableTextColor(string color)
        {
            if (!TryParseHexColor(color, out var red, out var green, out var blue))
            {
                return "#ffffff";
            }

            var luminance = (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
            return luminance > 150 ? "#111827" : "#ffffff";
        }

        // Parses a six-character hex color into red, green, and blue channel values.
        // color: Hex color value with or without a leading hash.
        // red: Parsed red channel value.
        // green: Parsed green channel value.
        // blue: Parsed blue channel value.
        // Returns true when parsing succeeds; otherwise, false.
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
