using System.Collections.Generic;

namespace StageProject_RaceCore.Models
{
    // ViewModel to represent a team's composition coming from the database
    public class TeamViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public string Color { get; set; }

        public int ActiveCyclistsCount { get; set; }
        public int BenchCyclistsCount { get; set; }

        public int TeamPoints => ActiveCyclistsCount * Team.TunicPoints;

        public List<CyclistSimple> ActiveCyclists { get; set; }
        public List<CyclistSimple> BenchCyclists { get; set; }
    }

    public class CyclistSimple
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsActive { get; set; }
    }
}
