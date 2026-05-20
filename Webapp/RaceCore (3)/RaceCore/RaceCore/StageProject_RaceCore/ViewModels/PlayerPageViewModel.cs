namespace StageProject_RaceCore.ViewModels
{
    public class PlayerPageViewModel
    {
        public string SearchTerm { get; set; } = string.Empty;
        public List<PlayerIndexViewModel> Players { get; set; } = new();

        public int TotalPlayers { get; set; }
        public int TotalPoints { get; set; }
        public int TotalSelections { get; set; }
        public int TotalDraftTurns { get; set; }
        public int TotalPointRecords { get; set; }
    }
}