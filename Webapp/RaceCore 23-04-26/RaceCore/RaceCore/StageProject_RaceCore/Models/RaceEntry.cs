namespace StageProject_RaceCore.Models
{
    public class RaceEntry
    {
        public int Id { get; set; }

        public int RaceId { get; set; }
        public Race Race { get; set; } = null!;

        public int CyclistId { get; set; }
        public Cyclist Cyclist { get; set; } = null!;

        public int? TeamId { get; set; }
        public Team? Team { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}