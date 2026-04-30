using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace StageProject_RaceCore.Models
{
    public class Team
    {
        public const int TunicPoints = 10;

        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Tag { get; set; } = string.Empty;

        public List<Cyclist> Cyclists { get; set; } = new();
        public List<RaceEntry> RaceEntries { get; set; } = new();

        public int ActiveCyclistsCount => Cyclists.Count(c => c.IsActive);
        public int BenchCyclistsCount => Cyclists.Count(c => !c.IsActive);
    }

    public class PlayerTeamsPageViewModel
    {
        public int GameId { get; set; }

        public int RaceId { get; set; }

        public string RaceName { get; set; } = "Geen actieve game";

        public string GameStatus { get; set; } = string.Empty;

        public int ActiveRiderSlots { get; set; } = 10;

        public int BenchRiderSlots { get; set; } = 5;

        public List<PlayerTeamViewModel> PlayerTeams { get; set; } = new();

        public bool HasGame => GameId > 0;

        public int PlayerCount => PlayerTeams.Count;

        public int FilledSlots => PlayerTeams.Sum(t => t.ActiveRiders.Count + t.BenchRiders.Count);

        public int TotalSlots => PlayerCount * (ActiveRiderSlots + BenchRiderSlots);
    }

    public class PlayerTeamViewModel
    {
        public int PlayerId { get; set; }

        public string PlayerName { get; set; } = string.Empty;

        public string Initials { get; set; } = "?";

        public int PositionInDraft { get; set; }

        public string Color { get; set; } = "#2563eb";

        public string ColorSoft { get; set; } = "rgba(37, 99, 235, 0.12)";

        public string ColorDark { get; set; } = "#1e40af";

        public string TextColor { get; set; } = "#ffffff";

        public List<PlayerTeamRiderViewModel> ActiveRiders { get; set; } = new();

        public List<PlayerTeamRiderViewModel> BenchRiders { get; set; } = new();
    }

    public class PlayerTeamRiderViewModel
    {
        public int CyclistId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string ProTeamName { get; set; } = string.Empty;

        public int PickNumber { get; set; }

        public bool IsActive { get; set; }
    }
}
