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

        [Range(1, 30, ErrorMessage = "Actieve renners moet tussen 1 en 30 zijn.")]
        public int RidersPerPlayer { get; set; } = 12;

        [Range(0, 30, ErrorMessage = "Bankrenners moet tussen 0 en 30 zijn.")]
        public int BenchPerPlayer { get; set; } = 6;

        public int TotalPicksPerPlayer => RidersPerPlayer + BenchPerPlayer;

        public List<SelectListItem> AvailableRaces { get; set; } = new();

        public List<PlayerSelectItemViewModel> AvailablePlayers { get; set; } = new();

        public int TotalStages { get; set; }

        public int TotalCyclists { get; set; }

        public int AvailableRaceCyclists { get; set; }
    }

    public class PlayerSelectItemViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public int PositionInDraft { get; set; }

        public bool IsSelected { get; set; }
    }
}