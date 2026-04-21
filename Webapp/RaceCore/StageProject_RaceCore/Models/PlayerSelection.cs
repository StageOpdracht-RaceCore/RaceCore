namespace StageProject_RaceCore.Models
{
    public class PlayerSelection
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public Player Player { get; set; }
        public int RaceId { get; set; }
        public Race Race { get; set; }
        public int CyclistId { get; set; }
        public Cyclist Cyclist { get; set; }
        public bool IsActive { get; set; }
    }
}
