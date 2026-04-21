using System.Collections.Generic;

namespace StageProject_RaceCore.Models
{
    public class TeamViewModel
    {
        public const int TunicPoints = 10;

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        public int ActiveCyclistsCount { get; set; }
        public int BenchCyclistsCount { get; set; }

        public int TeamPoints => ActiveCyclistsCount * TunicPoints;

        public List<CyclistSimple> ActiveCyclists { get; set; } = new();
        public List<CyclistSimple> BenchCyclists { get; set; } = new();
    }

    public class CyclistSimple
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}