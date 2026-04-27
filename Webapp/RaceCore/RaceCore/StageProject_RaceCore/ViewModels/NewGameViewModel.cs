using System.ComponentModel.DataAnnotations;

namespace StageProject_RaceCore.ViewModels
{
    public class NewGameViewModel
    {
        [Required]
        public string RaceName { get; set; } = "Tour de France";

        public int Year { get; set; } = DateTime.Now.Year;

        public int NumberOfStages { get; set; } = 21;

        public string PlayerNamesRaw { get; set; } = "";
    }
}