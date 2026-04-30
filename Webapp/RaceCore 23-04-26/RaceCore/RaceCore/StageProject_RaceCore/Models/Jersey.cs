namespace StageProject_RaceCore.Models
{
    public class Jersey
    {
        public int Id { get; set; }

        public int StageId { get; set; }
        public Stage Stage { get; set; } = null!;

        public int CyclistId { get; set; }
        public Cyclist Cyclist { get; set; } = null!;

        public string Type { get; set; } = string.Empty;
    }
}