namespace StageProject_RaceCore.Models
{
    public class PlayerPoints
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public Player Player { get; set; }
        public int RaceId { get; set; }
        public Race Race { get; set; }
        public int? StageId { get; set; }
        public Stage Stage { get; set; }
        public int CyclistId { get; set; }
        public Cyclist Cyclist { get; set; }
        public int Points { get; set; }
        public string Reason { get; set; }
    }
}
