using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace StageProject_RaceCore.Models
{
    // Team model aligned with the database schema (Name and Tag are required)
    public class Team
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Tag { get; set; }

        // Navigation property populated by EF Core (Cyclists table has TeamId FK)
        public List<Cyclist> Cyclists { get; set; }

        // Each tunic is worth 10 points (application rule, not a DB column)
        public const int TunicPoints = 10;

        // Computed properties to show active cyclists and those on the bench
        public int ActiveCyclistsCount => Cyclists?.Count(c => c.IsActive) ?? 0;
        public int BenchCyclistsCount => Cyclists?.Count(c => !c.IsActive) ?? 0;

        // Ensure Cyclists list is initialized and provide helpers to manage assignments
        public Team()
        {
            Cyclists = Cyclists ?? new List<Cyclist>();
        }

        public void AddCyclist(Cyclist cyclist)
        {
            if (cyclist == null) return;
            if (Cyclists == null) Cyclists = new List<Cyclist>();
            if (!Cyclists.Contains(cyclist))
            {
                Cyclists.Add(cyclist);
            }
            cyclist.Team = this;
            cyclist.TeamId = this.Id;
        }

        public void RemoveCyclist(Cyclist cyclist)
        {
            if (cyclist == null || Cyclists == null) return;
            Cyclists.Remove(cyclist);
            if (cyclist.Team == this) cyclist.Team = null;
            if (cyclist.TeamId == this.Id) cyclist.TeamId = null;
        }
    }
}
