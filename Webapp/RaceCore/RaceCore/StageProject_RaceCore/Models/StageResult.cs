namespace StageProject_RaceCore.Models
{
    public class StageResult
    {
        public int Id { get; set; }

        public int GameSessionId { get; set; }
        public GameSession GameSession { get; set; } = null!;

        public int StageId { get; set; }
        public Stage Stage { get; set; } = null!;

        public int CyclistId { get; set; }
        public Cyclist Cyclist { get; set; } = null!;

        public int? Position { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}