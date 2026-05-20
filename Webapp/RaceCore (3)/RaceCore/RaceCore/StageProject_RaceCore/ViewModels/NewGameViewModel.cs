using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace StageProject_RaceCore.ViewModels
{
    public class NewGameViewModel
    {
        [Required(ErrorMessage = "Choose a race.")]
        public int RaceId { get; set; }

        [Required(ErrorMessage = "Choose at least 2 players.")]
        public List<int> SelectedPlayerIds { get; set; } = new();

        [Range(1, 30, ErrorMessage = "Active riders must be between 1 and 30.")]
        public int RidersPerPlayer { get; set; } = 12;

        [Range(0, 30, ErrorMessage = "Bench riders must be between 0 and 30.")]
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