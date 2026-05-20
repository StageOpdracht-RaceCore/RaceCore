using System.ComponentModel.DataAnnotations;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.ViewModels
{
    public class RaceCreateViewModel
    {
        [Required(ErrorMessage = "Race name is required.")]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public int Year { get; set; } = DateTime.Now.Year;

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        public List<int> SelectedCyclistIds { get; set; } = new();

        public List<RaceStageInputViewModel> Stages { get; set; } = new();

        public List<Cyclist> AvailableCyclists { get; set; } = new();
    }

    public class RaceStageInputViewModel
    {
        public string Name { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime? Date { get; set; }
    }
}