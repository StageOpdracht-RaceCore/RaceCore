namespace StageProject_RaceCore.Models
{
    public class PointsRule
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public int? FromPosition { get; set; }
        public int? ToPosition { get; set; }
        public int Points { get; set; }
    }
}
