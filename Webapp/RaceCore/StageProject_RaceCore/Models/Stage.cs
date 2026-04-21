namespace StageProject_RaceCore.Models
{
    public class Stage
    {
        public int Id { get; set; }
        public int RaceId { get; set; }
        public Race Race { get; set; }
        public int StageNumber { get; set; }
        public string Name { get; set; }
        public DateTime? Date { get; set; }
    }
}
