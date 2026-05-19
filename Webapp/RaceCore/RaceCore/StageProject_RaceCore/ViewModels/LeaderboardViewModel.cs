using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.ViewModels
{
    public class LeaderboardViewModel
    {
        public int SelectedGameId { get; set; }

        public string RaceName { get; set; } = "No active game";

        public string GameStatus { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public int PlayerCount { get; set; }

        public int TotalPoints { get; set; }

        public List<GameSession> Games { get; set; } = new();

        public List<LeaderboardPlayerColumnViewModel> Players { get; set; } = new();

        public List<LeaderboardStageRowViewModel> StageRows { get; set; } = new();

        public Dictionary<int, int> FinalSettlementPoints { get; set; } = new();

        public Dictionary<int, int> TotalPointsPerPlayer { get; set; } = new();
    }

    public class LeaderboardPlayerColumnViewModel
    {
        public int PlayerId { get; set; }

        public string PlayerName { get; set; } = "";

        public string Color { get; set; } = "#2563eb";
    }

    public class LeaderboardStageRowViewModel
    {
        public int StageId { get; set; }

        public int StageNumber { get; set; }

        public string StageName { get; set; } = "";

        public Dictionary<int, int> PointsPerPlayer { get; set; } = new();
    }
}