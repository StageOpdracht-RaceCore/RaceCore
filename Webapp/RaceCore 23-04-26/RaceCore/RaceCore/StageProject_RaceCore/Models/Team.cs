using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace StageProject_RaceCore.Models
{
    public class Team
    {
        public const int TunicPoints = 10;

        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Tag { get; set; } = string.Empty;

        public List<Cyclist> Cyclists { get; set; } = new();
        public List<RaceEntry> RaceEntries { get; set; } = new();

        public int ActiveCyclistsCount => Cyclists.Count(c => c.IsActive);
        public int BenchCyclistsCount => Cyclists.Count(c => !c.IsActive);
    }
}