namespace StageProject_RaceCore.Models
{
    /* =========================================================
       DraftTurn.cs

       Dit model stelt één beurt voor in de draft.
       Elke beurt hoort bij:
       - een race
       - een speler
       - eventueel een gekozen wielrenner
       - een game sessie
       ========================================================= */

    public class DraftTurn
    {
        // Unieke id van de draft beurt
        public int Id { get; set; }

        // Race waartoe deze draft beurt behoort
        public int RaceId { get; set; }
        public Race Race { get; set; } = null!;

        // Volgorde van de beurt in de draft
        public int TurnNumber { get; set; }

        // Speler die aan de beurt is
        public int PlayerId { get; set; }
        public Player Player { get; set; } = null!;

        // Gekozen wielrenner, kan leeg zijn zolang de beurt nog niet gedaan is
        public int? CyclistId { get; set; }
        public Cyclist? Cyclist { get; set; }

        // Game sessie waartoe deze draft beurt behoort
        public int GameSessionId { get; set; }
        public GameSession GameSession { get; set; } = null!;
    }
}