namespace StageProject_RaceCore.Models
{
    public class GameSession
    {
        public int Id { get; set; }

        public int RaceId { get; set; }
        public Race Race { get; set; } = null!;

        public int StageId { get; set; }
        public Stage Stage { get; set; } = null!;

        public string Status { get; set; } = "Draft";

        public int CurrentStageNumber { get; set; } = 0;

        public int RidersPerPlayer { get; set; } = 12;

        public int BenchPerPlayer { get; set; } = 6;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? HostSessionId { get; set; }

        public DateTime? LastHostPingAt { get; set; }
    }
}