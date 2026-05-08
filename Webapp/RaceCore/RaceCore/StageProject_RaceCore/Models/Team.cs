using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace StageProject_RaceCore.Models
{
    // Represents a cycling team with its riders and race participation records.
    public class Team
    {
        // Points awarded for a tunic result.
        public const int TunicPoints = 10;

        // Unique database identifier for the team.
        public int Id { get; set; }

        // Full team name shown in the application.
        [Required]
        public string Name { get; set; } = string.Empty;

        // Short team code or abbreviation.
        [Required]
        public string Tag { get; set; } = string.Empty;

        // Cyclists currently linked to this team.
        public List<Cyclist> Cyclists { get; set; } = new();
        // Race entries where this team participates.
        public List<RaceEntry> RaceEntries { get; set; } = new();

        // Number of linked cyclists marked as active.
        public int ActiveCyclistsCount => Cyclists.Count(c => c.IsActive);
        // Number of linked cyclists currently marked as bench riders.
        public int BenchCyclistsCount => Cyclists.Count(c => !c.IsActive);
    }

    // Page-level model used by the Teams overview view.
    public class PlayerTeamsPageViewModel
    {
        // Current game session identifier.
        public int GameId { get; set; }

        // Race identifier connected to the current game.
        public int RaceId { get; set; }

        // Display name for the selected race.
        public string RaceName { get; set; } = "Geen actieve game";

        // Status label for the current game.
        public string GameStatus { get; set; } = string.Empty;

        // Number of active rider slots shown for each player.
        public int ActiveRiderSlots { get; set; } = 10;

        // Number of bench rider slots shown for each player.
        public int BenchRiderSlots { get; set; } = 5;

        // Team rosters grouped by player.
        public List<PlayerTeamViewModel> PlayerTeams { get; set; } = new();

        // Indicates whether the page is tied to an existing game.
        public bool HasGame => GameId > 0;

        // Number of players with a team in the overview.
        public int PlayerCount => PlayerTeams.Count;

        // Total number of rider slots currently filled across all players.
        public int FilledSlots => PlayerTeams.Sum(t => t.ActiveRiders.Count + t.BenchRiders.Count);

        // Total rider capacity across all player teams.
        public int TotalSlots => PlayerCount * (ActiveRiderSlots + BenchRiderSlots);
    }

    // View model for one player's active and bench rosters.
    public class PlayerTeamViewModel
    {
        // Player identifier used when saving swaps.
        public int PlayerId { get; set; }

        // Player name shown on the team card.
        public string PlayerName { get; set; } = string.Empty;

        // Short label shown inside the player avatar.
        public string Initials { get; set; } = "?";

        // Player's draft order position.
        public int PositionInDraft { get; set; }

        // Main player color used for card accents.
        public string Color { get; set; } = "#2563eb";

        // Soft version of the player color used for row backgrounds.
        public string ColorSoft { get; set; } = "rgba(37, 99, 235, 0.12)";

        // Dark version of the player color used for gradients.
        public string ColorDark { get; set; } = "#1e40af";

        // Foreground color chosen for readable text on the player color.
        public string TextColor { get; set; } = "#ffffff";

        // Riders currently assigned to active slots.
        public List<PlayerTeamRiderViewModel> ActiveRiders { get; set; } = new();

        // Riders currently assigned to bench slots.
        public List<PlayerTeamRiderViewModel> BenchRiders { get; set; } = new();
    }

    // View model for one rider row in a player roster.
    public class PlayerTeamRiderViewModel
    {
        // Cyclist identifier used by the swap endpoint.
        public int CyclistId { get; set; }

        // Full rider name shown in the roster.
        public string FullName { get; set; } = string.Empty;

        // Professional team name shown next to the rider.
        public string ProTeamName { get; set; } = string.Empty;

        // Draft pick number used for ordering and display.
        public int PickNumber { get; set; }

        // Indicates whether this rider is in an active slot.
        public bool IsActive { get; set; }
    }
}
