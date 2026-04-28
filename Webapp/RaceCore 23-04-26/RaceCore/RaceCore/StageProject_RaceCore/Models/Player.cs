namespace StageProject_RaceCore.Models
{
    public class Player
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int PositionInDraft { get; set; }

        public int TotalPoints { get; set; } = 0;

        public ICollection<PlayerSelection> Selections { get; set; } = new List<PlayerSelection>();

        public ICollection<DraftTurn> DraftTurns { get; set; } = new List<DraftTurn>();
        public ICollection<PlayerPoints> PlayerPoints { get; set; } = new List<PlayerPoints>();
    }
}