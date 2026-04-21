namespace StageProject_RaceCore.Models
{
    public class Jersey
    {
        public int Id { get; set; }
        public int StageId { get; set; }
        public Stage Stage { get; set; }
        public int CyclistId { get; set; }
        public Cyclist Cyclist { get; set; }
        public string Type { get; set; } // Yellow, Green, etc.
    }
}
