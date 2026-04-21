using System.Linq;

namespace StageProject_RaceCore.Models
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public string Color { get; set; }
        public List<Cyclist> Cyclists { get; set; }

        // Each tunic is worth 10 points
        public const int TunicPoints = 10;

        // Computed properties to show active cyclists and those on the bench
        public int ActiveCyclistsCount => Cyclists?.Count(c => c.IsActive) ?? 0;
        public int BenchCyclistsCount => Cyclists?.Count(c => !c.IsActive) ?? 0;
    }
}
