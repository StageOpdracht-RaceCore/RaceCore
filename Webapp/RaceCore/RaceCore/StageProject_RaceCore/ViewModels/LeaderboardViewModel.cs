using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.ViewModels
{
    public class LeaderboardViewModel
    {
        public int SelectedGameId { get; set; }

        public string RaceName { get; set; } = "Geen actieve game";

        public string GameStatus { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public int PlayerCount { get; set; }

        public int TotalPoints { get; set; }

        public int HighestPoints { get; set; }

        public List<GameSession> Games { get; set; } = new();

        public List<LeaderboardRowViewModel> Rows { get; set; } = new();
    }

    public class LeaderboardRowViewModel
    {
        public int Rank { get; set; }

        public int PlayerId { get; set; }

        public string PlayerName { get; set; } = "";

        public string Initials { get; set; } = "?";

        public int DraftPosition { get; set; }

        public int TotalPoints { get; set; }

        public int RidersCount { get; set; }

        public string BestCyclistName { get; set; } = "-";

        public int BestCyclistPoints { get; set; }

        public string Color { get; set; } = "#2563eb";
    }
}