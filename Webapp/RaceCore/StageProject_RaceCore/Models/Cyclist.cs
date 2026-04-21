namespace StageProject_RaceCore.Models
{
    public class Cyclist
    {
        public int Id { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        public int? TeamId { get; set; }
        public Team? Team { get; set; }

        public bool IsActive { get; set; }

        public List<PlayerSelection> PlayerSelections { get; set; } = new();
        public List<RaceEntry> RaceEntries { get; set; } = new();
        public List<StageResult> StageResults { get; set; } = new();
        public List<DraftTurn> DraftTurns { get; set; } = new();
        public List<Jersey> Jerseys { get; set; } = new();
        public List<PlayerPoints> PlayerPoints { get; set; } = new();

        public string TeamName => Team?.Name ?? "No team";
        public string FullName => $"{FirstName} {LastName}";
    }
}