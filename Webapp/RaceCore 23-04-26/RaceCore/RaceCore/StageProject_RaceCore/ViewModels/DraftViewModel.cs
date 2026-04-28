namespace StageProject_RaceCore.ViewModels
{
    public class DraftTurnViewModel
    {
        public int Id { get; set; }

        public int TurnNumber { get; set; }

        public string PlayerName { get; set; } = string.Empty;

        public int? CyclistId { get; set; }

        public string? CyclistName { get; set; }
    }
}