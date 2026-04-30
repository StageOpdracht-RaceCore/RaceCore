using System.ComponentModel.DataAnnotations;

namespace StageProject_RaceCore.Models
{
    public class Race
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public int Year { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public ICollection<Stage> Stages { get; set; } = new List<Stage>();
        public ICollection<RaceEntry> RaceEntries { get; set; } = new List<RaceEntry>();
        public ICollection<PlayerSelection> PlayerSelections { get; set; } = new List<PlayerSelection>();
        public ICollection<DraftTurn> DraftTurns { get; set; } = new List<DraftTurn>();
    }
}