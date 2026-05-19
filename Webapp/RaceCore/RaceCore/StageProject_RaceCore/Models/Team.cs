using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace StageProject_RaceCore.Models
{
    /// <summary>
    /// Represents a real-world professional cycling team (e.g. "Team Visma | Lease a Bike").
    /// Cyclists belong to a Team; a Team is not the same as a player's fantasy squad.
    /// </summary>
    public class Team
    {
        /// <summary>Points awarded when a cyclist wins a stage wearing the leader's tunic.</summary>
        public const int TunicPoints = 10;

        public int Id { get; set; }

        /// <summary>Full name of the pro team, e.g. "Team Visma | Lease a Bike".</summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>Short identifier used in standings, e.g. "VIS".</summary>
        [Required]
        public string Tag { get; set; } = string.Empty;

        /// <summary>All cyclists registered under this pro team.</summary>
        public List<Cyclist> Cyclists { get; set; } = new();

        /// <summary>All race entries where this team participated.</summary>
        public List<RaceEntry> RaceEntries { get; set; } = new();

        /// <summary>Number of cyclists on this team who are currently active in a game selection.</summary>
        public int ActiveCyclistsCount => Cyclists.Count(c => c.IsActive);

        /// <summary>Number of cyclists on this team who are currently on the bench.</summary>
        public int BenchCyclistsCount => Cyclists.Count(c => !c.IsActive);
    }

    /// <summary>
    /// Top-level view model for the Teams page.
    /// Carries all data needed to render the full page: game context, slot config, and all player teams.
    /// </summary>
    public class PlayerTeamsPageViewModel
    {
        /// <summary>ID of the currently active game session (0 if no game is active).</summary>
        public int GameId { get; set; }

        /// <summary>ID of the race associated with the active game session.</summary>
        public int RaceId { get; set; }

        /// <summary>
        /// Display name shown in the banner section, e.g. "Tour de France 2024".
        /// Defaults to a fallback message when no game is active.
        /// </summary>
        public string RaceName { get; set; } = "No active game";

        /// <summary>Current lifecycle status of the game, e.g. "Draft", "Active", "Finished".</summary>
        public string GameStatus { get; set; } = string.Empty;

        /// <summary>Maximum number of active (starting) riders each player is allowed to field.</summary>
        public int ActiveRiderSlots { get; set; } = 10;

        /// <summary>Maximum number of bench (reserve) riders each player is allowed to keep.</summary>
        public int BenchRiderSlots { get; set; } = 5;

        /// <summary>One entry per player, each containing their full roster.</summary>
        public List<PlayerTeamViewModel> PlayerTeams { get; set; } = new();

        /// <summary>True when an actual game session is loaded (GameId was resolved from the database).</summary>
        public bool HasGame => GameId > 0;

        /// <summary>Total number of players participating in the current game.</summary>
        public int PlayerCount => PlayerTeams.Count;

        /// <summary>Total number of rider slots that have been filled across all player teams.</summary>
        public int FilledSlots => PlayerTeams.Sum(t => t.ActiveRiders.Count + t.BenchRiders.Count);

        /// <summary>Maximum number of disqualified rider slots (for consistent table layout).</summary>
        public int DisqualifiedRiderSlots { get; set; }

        /// <summary>
        /// Maximum possible filled slots: every player filling every active + bench slot.
        /// Used to render the "Filled / Total" stat.
        /// </summary>
        public int TotalSlots => PlayerCount * (ActiveRiderSlots + BenchRiderSlots);
    }

    /// <summary>
    /// View model for a single player's fantasy team within the Teams page.
    /// Contains identity info, colour theming, and the player's roster split by active/bench/DQ.
    /// </summary>
    public class PlayerTeamViewModel
    {
        public int PlayerId { get; set; }

        public string PlayerName { get; set; } = string.Empty;

        /// <summary>Two-letter initials derived from the player's name, shown in the avatar bubble.</summary>
        public string Initials { get; set; } = "?";

        /// <summary>The player's position in the snake draft order (1 = first pick overall).</summary>
        public int PositionInDraft { get; set; }

        /// <summary>Primary accent colour for this player's card, as a CSS hex string.</summary>
        public string Color { get; set; } = "#2563eb";

        /// <summary>Low-opacity version of the primary colour used for row backgrounds.</summary>
        public string ColorSoft { get; set; } = "rgba(37, 99, 235, 0.12)";

        /// <summary>Darker shade of the primary colour used for gradients and hover states.</summary>
        public string ColorDark { get; set; } = "#1e40af";

        /// <summary>Foreground colour (white or near-black) chosen for legible contrast on the primary colour.</summary>
        public string TextColor { get; set; } = "#ffffff";

        /// <summary>Cyclists currently in the starting lineup (IsActive = true), ordered by draft turn.</summary>
        public List<PlayerTeamRiderViewModel> ActiveRiders { get; set; } = new();

        /// <summary>Cyclists currently on the bench (IsActive = false), ordered by draft turn.</summary>
        public List<PlayerTeamRiderViewModel> BenchRiders { get; set; } = new();

        /// <summary>Cyclists that have been disqualified (tracked in-memory on the server).</summary>
        public List<PlayerTeamRiderViewModel> DisqualifiedRiders { get; set; } = new();
    }

    /// <summary>
    /// View model for a single cyclist row inside a player's roster table.
    /// </summary>
    public class PlayerTeamRiderViewModel
    {
        public int CyclistId { get; set; }

        /// <summary>First + last name combined, e.g. "Wout van Aert".</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Name of the pro team this cyclist rides for, e.g. "Team Visma | Lease a Bike".</summary>
        public string ProTeamName { get; set; } = string.Empty;

        /// <summary>The draft turn number on which this cyclist was picked (used for sorting).</summary>
        public int PickNumber { get; set; }

        /// <summary>True when the cyclist is in the starting lineup; false when on the bench.</summary>
        public bool IsActive { get; set; }

        /// <summary>True when the cyclist has been disqualified.</summary>
        public bool IsDisqualified { get; set; }
    }
}
