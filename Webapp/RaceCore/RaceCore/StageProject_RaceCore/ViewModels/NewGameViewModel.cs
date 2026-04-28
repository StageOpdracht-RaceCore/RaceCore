using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace StageProject_RaceCore.ViewModels
{
    public class NewGameViewModel
    {
        [Required(ErrorMessage = "Kies een race.")]
        public int RaceId { get; set; }

        [Required(ErrorMessage = "Kies minstens 2 spelers.")]
        public List<int> SelectedPlayerIds { get; set; } = new();

        public List<SelectListItem> AvailableRaces { get; set; } = new();

        public List<PlayerSelectItemViewModel> AvailablePlayers { get; set; } = new();

        public int TotalStages { get; set; }

        public int TotalCyclists { get; set; }
    }

    public class PlayerSelectItemViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public int PositionInDraft { get; set; }

        public bool IsSelected { get; set; }
    }
}