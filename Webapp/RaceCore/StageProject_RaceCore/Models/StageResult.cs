namespace StageProject_RaceCore.Models
{
    public class StageResult
    {
        public int Id { get; set; }
        public int StageId { get; set; }
        public Stage Stage { get; set; }
        public int CyclistId { get; set; }
        public Cyclist Cyclist { get; set; }
        public int? Position { get; set; }
        public string Status { get; set; }
    }
}
