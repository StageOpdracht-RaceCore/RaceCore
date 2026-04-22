namespace StageProject_RaceCore.Models
{
    public class DashboardViewModel
    {
        public int PlayersCount { get; set; }
        public int CyclistsCount { get; set; }
        public int TeamsCount { get; set; }
        public int StagesCount { get; set; }

        public List<PlayerRankingItem> PlayerRanking { get; set; } = new();
        public List<TopCyclistItem> TopCyclists { get; set; } = new();
        public List<JerseyItem> Jerseys { get; set; } = new();

        public string? LatestStageTitle { get; set; }
        public List<string> LatestStageTop3 { get; set; } = new();

        public bool DraftCompleted { get; set; }
        public int TotalDraftPicks { get; set; }
    }

    public class PlayerRankingItem
    {
        public int Position { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int Points { get; set; }
    }

    public class TopCyclistItem
    {
        public string Name { get; set; } = string.Empty;
        public int Points { get; set; }
    }

    public class JerseyItem
    {
        public string Type { get; set; } = string.Empty;
        public string CyclistName { get; set; } = string.Empty;
    }
}