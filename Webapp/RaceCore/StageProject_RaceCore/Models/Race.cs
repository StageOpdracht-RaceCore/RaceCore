namespace StageProject_RaceCore.Models
{
    public class Race
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Year { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<Stage> Stages { get; set; }
    }
}
