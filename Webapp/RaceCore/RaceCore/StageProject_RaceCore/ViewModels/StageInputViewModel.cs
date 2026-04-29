using Microsoft.AspNetCore.Mvc.Rendering;

namespace StageProject_RaceCore.ViewModels
{
    public class StageInputViewModel
    {
        public int GameId { get; set; }
        public int StageId { get; set; }

        public string RaceName { get; set; } = "";
        public string StageName { get; set; } = "";

        public List<SelectListItem> Cyclists { get; set; } = new();
        public List<StageInputRow> Rows { get; set; } = new();

        public int? YellowJerseyCyclistId { get; set; }
        public int? GreenJerseyCyclistId { get; set; }
        public int? PolkaJerseyCyclistId { get; set; }
        public int? WhiteJerseyCyclistId { get; set; }
    }

    public class StageInputRow
    {
        public int Position { get; set; }
        public int? CyclistId { get; set; }
    }
}