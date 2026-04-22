namespace StageProject_RaceCore.Models
{
    public class Player
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<PlayerSelection> Selections { get; set; } = new();
        public List<DraftTurn> DraftTurns { get; set; } = new();
        public List<PlayerPoints> PlayerPoints { get; set; } = new();
    }
}