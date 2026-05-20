using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace StageProject_RaceCore.Models
{
    // Represents a real-world professional cycling team (e.g. Jumbo-Visma).
    // Cyclists belong to a Team; a Team is not the same as a player's fantasy squad.
    public class Team
    {
        // Points awarded when a cyclist wins a stage wearing the leader's tunic.
        public const int TunicPoints = 10;

        public int Id { get; set; }

        // Full name of the pro team, e.g. "Team Visma | Lease a Bike".
        [Required]
        public string Name { get; set; } = string.Empty;

        // Short identifier used in standings, e.g. "VIS".
        [Required]
        public string Tag { get; set; } = string.Empty;

        // All cyclists registered under this pro team.
        public List<Cyclist> Cyclists { get; set; } = new();

        // All race entries where this team participated.
        public List<RaceEntry> RaceEntries { get; set; } = new();

        // Number of cyclists on this team who are currently active in a game selection.
        public int ActiveCyclistsCount => Cyclists.Count(c => c.IsActive);

        // Number of cyclists on this team who are currently on the bench.
        public int BenchCyclistsCount => Cyclists.Count(c => !c.IsActive);
    }

    // Top-level view model for the Teams page.
    // Carries all data needed to render the full page: game context, slot config, and all player teams.
    public class PlayerTeamsPageViewModel
    {
        // ID of the currently active game session (0 if no game is active).
        public int GameId { get; set; }

        // ID of the race associated with the active game session.
        public int RaceId { get; set; }

        // Display name shown in the banner section, e.g. "Tour de France 2024".
        // Defaults to a fallback message when no game is active.
        public string RaceName { get; set; } = "No active game";

        // Current lifecycle status of the game, e.g. "Draft", "Active", "Finished".
        public string GameStatus { get; set; } = string.Empty;

        // Maximum number of active (starting) riders each player is allowed to field.
        public int ActiveRiderSlots { get; set; } = 10;

        // Maximum number of bench (reserve) riders each player is allowed to keep.
        public int BenchRiderSlots { get; set; } = 5;

        // One entry per player, each containing their full roster.
        public List<PlayerTeamViewModel> PlayerTeams { get; set; } = new();

        // True when an actual game session is loaded (GameId was resolved from the database).
        public bool HasGame => GameId > 0;

        // Total number of players participating in the current game.
        public int PlayerCount => PlayerTeams.Count;

        // Total number of rider slots that have been filled across all player teams.
        public int FilledSlots => PlayerTeams.Sum(t => t.ActiveRiders.Count + t.BenchRiders.Count);

        // Maximum number of disqualified rider slots (for consistent table layout).
        public int DisqualifiedRiderSlots { get; set; }

        // Maximum possible filled slots: every player filling every active + bench slot.
        public int TotalSlots => PlayerCount * (ActiveRiderSlots + BenchRiderSlots);
    }

    // View model for a single player's fantasy team within the Teams page.
    // Contains identity info, colour theming, and the player's roster split by active/bench.
    public class PlayerTeamViewModel
    {
        public int PlayerId { get; set; }

        public string PlayerName { get; set; } = string.Empty;

        // Two-letter initials derived from the player's name, shown in the avatar bubble.
        public string Initials { get; set; } = "?";

        // The player's position in the snake draft order (1 = first pick overall).
        public int PositionInDraft { get; set; }

        // Primary accent colour for this player's card, as a CSS hex string.
        public string Color { get; set; } = "#2563eb";

        // Low-opacity version of the primary colour used for row backgrounds.
        public string ColorSoft { get; set; } = "rgba(37, 99, 235, 0.12)";

        // Darker shade of the primary colour used for gradients and hover states.
        public string ColorDark { get; set; } = "#1e40af";

        // Foreground colour (white or near-black) chosen for legible contrast on the primary colour.
        public string TextColor { get; set; } = "#ffffff";

        // Cyclists currently in the starting lineup (IsActive = true), ordered by draft turn.
        public List<PlayerTeamRiderViewModel> ActiveRiders { get; set; } = new();

        // Cyclists currently on the bench (IsActive = false), ordered by draft turn.
        public List<PlayerTeamRiderViewModel> BenchRiders { get; set; } = new();

        // Cyclists that have been disqualified.
        public List<PlayerTeamRiderViewModel> DisqualifiedRiders { get; set; } = new();
    }

    // View model for a single cyclist row inside a player's roster table.
    public class PlayerTeamRiderViewModel
    {
        public int CyclistId { get; set; }

        // First + last name combined, e.g. "Wout van Aert".
        public string FullName { get; set; } = string.Empty;

        // Name of the pro team this cyclist rides for, e.g. "Team Visma | Lease a Bike".
        public string ProTeamName { get; set; } = string.Empty;

        // The draft turn number on which this cyclist was picked (used for sorting).
        public int PickNumber { get; set; }

        // True when the cyclist is in the starting lineup; false when on the bench.
        public bool IsActive { get; set; }

        // True when the cyclist has been disqualified.
        public bool IsDisqualified { get; set; }
    }
}
