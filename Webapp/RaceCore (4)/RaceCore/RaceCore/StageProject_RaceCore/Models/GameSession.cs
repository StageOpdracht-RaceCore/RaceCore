namespace StageProject_RaceCore.Models
{
    /* =========================================================
       GameSession.cs

       Dit model stelt één game voor.
       Hierin bewaren we welke race gespeeld wordt,
       wat de status is en hoeveel renners per speler
       gekozen mogen worden.
       ========================================================= */

    public class GameSession
    {
        // Unieke id van de game
        public int Id { get; set; }

        // Race die gekoppeld is aan deze game
        public int RaceId { get; set; }
        public Race Race { get; set; } = null!;

        // Status van de game, standaard begint dit als Draft
        public string Status { get; set; } = "Draft";

        // Huidige etappe nummer
        public int CurrentStageNumber { get; set; } = 0;

        // Aantal actieve renners per speler
        public int RidersPerPlayer { get; set; } = 12;

        // Aantal bank renners per speler
        public int BenchPerPlayer { get; set; } = 6;

        // Datum waarop de game is aangemaakt
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Session id van de host
        public string? HostSessionId { get; set; }

        // Laatste moment dat de host actief was
        public DateTime? LastHostPingAt { get; set; }
    }
}