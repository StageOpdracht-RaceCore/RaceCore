using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace StageProject_RaceCore.ViewModels
{
    /* =========================================================
       NewGameViewModel.cs

       Dit ViewModel wordt gebruikt op de New Game pagina.
       Hierin bewaren we de gekozen race, gekozen spelers
       en de instellingen voor de draft.
       ========================================================= */

    public class NewGameViewModel
    {
        // Gekozen race is verplicht
        [Required(ErrorMessage = "Choose a race.")]
        public int RaceId { get; set; }

        // Gekozen spelers, minimum 2 spelers nodig
        [Required(ErrorMessage = "Choose at least 2 players.")]
        public List<int> SelectedPlayerIds { get; set; } = new();

        // Aantal actieve renners per speler
        [Range(1, 30, ErrorMessage = "Active riders must be between 1 and 30.")]
        public int RidersPerPlayer { get; set; } = 12;

        // Aantal bank renners per speler
        [Range(0, 30, ErrorMessage = "Bench riders must be between 0 and 30.")]
        public int BenchPerPlayer { get; set; } = 6;

        // Totaal aantal picks per speler
        public int TotalPicksPerPlayer => RidersPerPlayer + BenchPerPlayer;

        // Beschikbare races voor dropdown
        public List<SelectListItem> AvailableRaces { get; set; } = new();

        // Beschikbare spelers om te selecteren
        public List<PlayerSelectItemViewModel> AvailablePlayers { get; set; } = new();

        // Aantal etappes van de gekozen race
        public int TotalStages { get; set; }

        // Totaal aantal wielrenners in de database
        public int TotalCyclists { get; set; }

        // Aantal beschikbare wielrenners voor deze race
        public int AvailableRaceCyclists { get; set; }
    }

    /* =========================================================
       PlayerSelectItemViewModel.cs

       Dit model wordt gebruikt om spelers te tonen
       op de New Game pagina.
       ========================================================= */

    public class PlayerSelectItemViewModel
    {
        // Id van de speler
        public int Id { get; set; }

        // Naam van de speler
        public string Name { get; set; } = "";

        // Positie in de draft volgorde
        public int PositionInDraft { get; set; }

        // Of de speler geselecteerd is
        public bool IsSelected { get; set; }
    }
}