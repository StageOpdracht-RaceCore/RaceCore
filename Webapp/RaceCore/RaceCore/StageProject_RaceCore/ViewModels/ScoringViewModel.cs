namespace StageProject_RaceCore.ViewModels
{
    public class ScoringViewModel
    {
        public int StageId { get; set; }
        public int RaceId { get; set; }
        public string StageName { get; set; }

        // Lijst voor de top 25 invoer
        public List<StageResultInput> Results { get; set; } = new List<StageResultInput>();

        // Voor de dropdowns
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AvailableCyclists { get; set; }
    }

    public class StageResultInput
    {
        public int Position { get; set; }
        public int? CyclistId { get; set; }
        public bool HasYellowJersey { get; set; }
        public bool HasGreenJersey { get; set; }
        public bool HasPolkaJersey { get; set; }
        public bool HasWhiteJersey { get; set; }
    }
}