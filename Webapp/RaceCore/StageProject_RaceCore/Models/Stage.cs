namespace StageProject_RaceCore.Models
{
    public class Stage
    {
        public int Id { get; set; }

        public int RaceId { get; set; }
        public Race Race { get; set; } = null!;

        public int StageNumber { get; set; }

        public string Name { get; set; } = string.Empty;
        public DateTime? Date { get; set; }

        public List<StageResult> Results { get; set; } = new();
        public List<Jersey> Jerseys { get; set; } = new();
        public List<PlayerPoints> PlayerPoints { get; set; } = new();
    }
}