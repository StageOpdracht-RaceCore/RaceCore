namespace StageProject_RaceCore.ViewModels
{
    public class HistoryViewModel
    {
        public int RaceId { get; set; }
        public string RaceName { get; set; }
        public List<StageHistoryItem> Stages { get; set; } = new();
    }

    public class StageHistoryItem
    {
        public int StageId { get; set; }
        public int StageNumber { get; set; }
        public string StageName { get; set; }
        public DateTime? Date { get; set; } // Voeg het vraagteken toe

        // De renner die de rit won
        public string WinnerName { get; set; }
        public string WinnerTeam { get; set; }

        // De speler die de meeste punten pakte in deze rit
        public string TopPlayerName { get; set; }
        public int TopPlayerPoints { get; set; }
    }
}