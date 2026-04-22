namespace StageProject_RaceCore.Models
{
    public class PlayerSelection
    {
        public int Id { get; set; }

        public int PlayerId { get; set; }
        public Player Player { get; set; } = null!;

        public int RaceId { get; set; }
        public Race Race { get; set; } = null!;

        public int CyclistId { get; set; }
        public Cyclist Cyclist { get; set; } = null!;

        public bool IsActive { get; set; }
    }
}