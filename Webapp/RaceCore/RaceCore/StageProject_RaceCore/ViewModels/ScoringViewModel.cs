using Microsoft.AspNetCore.Mvc.Rendering;

namespace StageProject_RaceCore.ViewModels
{
    public class ScoringViewModel
    {
        public int StageId { get; set; }

        public List<SelectListItem> AvailableCyclists { get; set; } = new();

        public List<StageResultViewModel> Results { get; set; } = new();

        public int? YellowOutsideTop25CyclistId { get; set; }
        public int? GreenOutsideTop25CyclistId { get; set; }
        public int? PolkaOutsideTop25CyclistId { get; set; }
        public int? WhiteOutsideTop25CyclistId { get; set; }
    }

    public class StageResultViewModel
    {
        public int Position { get; set; }

        public int? CyclistId { get; set; }

        public string? CyclistName { get; set; }

        public bool HasYellowJersey { get; set; }

        public bool HasGreenJersey { get; set; }

        public bool HasPolkaJersey { get; set; }

        public bool HasWhiteJersey { get; set; }
    }
}