using System.ComponentModel.DataAnnotations;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore.ViewModels
{
    public class RaceEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Race name is required.")]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public int Year { get; set; }

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        public List<int> SelectedCyclistIds { get; set; } = new();

        public List<RaceStageInputViewModel> Stages { get; set; } = new();

        public List<Cyclist> AvailableCyclists { get; set; } = new();
    }
}