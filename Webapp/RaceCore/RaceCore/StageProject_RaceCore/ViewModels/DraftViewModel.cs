namespace StageProject_RaceCore.ViewModels
{
    /* =========================================================
       DraftTurnViewModel.cs

       Dit ViewModel wordt gebruikt om draft beurten
       naar de view te sturen.
       ========================================================= */

    public class DraftTurnViewModel
    {
        // Id van de draft beurt
        public int Id { get; set; }

        // Volgnummer van de beurt
        public int TurnNumber { get; set; }

        // Id van de speler die aan de beurt is
        public int PlayerId { get; set; }

        // Naam van de speler
        public string PlayerName { get; set; } = string.Empty;

        // Id van de gekozen wielrenner, kan leeg zijn
        public int? CyclistId { get; set; }

        // Naam van de gekozen wielrenner, kan leeg zijn
        public string? CyclistName { get; set; }
    }
}