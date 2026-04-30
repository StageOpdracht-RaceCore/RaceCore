namespace StageProject_RaceCore.ViewModels
{
    public class PlayerIndexViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int PositionInDraft { get; set; }
        public int TotalPoints { get; set; }

        public int SelectionsCount { get; set; }
        public int DraftTurnsCount { get; set; }
        public int PointsRecordsCount { get; set; }
    }
}