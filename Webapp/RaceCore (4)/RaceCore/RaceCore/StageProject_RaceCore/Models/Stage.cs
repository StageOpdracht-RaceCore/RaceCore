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

        public double? StartLat { get; set; }
        public double? StartLng { get; set; }
        public double? EndLat { get; set; }
        public double? EndLng { get; set; }

        public ICollection<StageResult> Results { get; set; } = new List<StageResult>();

        public ICollection<PlayerPoints> PlayerPoints { get; set; } = new List<PlayerPoints>();
    }
}