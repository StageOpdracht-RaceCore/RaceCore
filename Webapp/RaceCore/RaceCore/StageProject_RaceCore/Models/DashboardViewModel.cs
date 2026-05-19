namespace StageProject_RaceCore.Models
{
    /* =========================================================
       DashboardViewModel.cs

       Dit model wordt gebruikt voor de dashboard pagina.
       Hier worden alle gegevens opgeslagen die we naar
       de dashboard view willen sturen.
       ========================================================= */

    public class DashboardViewModel
    {
        /* =========================================================
           ALGEMENE STATISTIEKEN
           ========================================================= */

        // Aantal spelers in de huidige game
        public int PlayersCount { get; set; }

        // Aantal gekozen wielrenners
        public int CyclistsCount { get; set; }

        // Aantal teams in de database
        public int TeamsCount { get; set; }

        // Aantal etappes van de race
        public int StagesCount { get; set; }

        /* =========================================================
           DASHBOARD LIJSTEN
           ========================================================= */

        // Ranking van alle spelers
        public List<PlayerRankingItem> PlayerRanking { get; set; } = new();

        // Beste wielrenners op basis van punten
        public List<TopCyclistItem> TopCyclists { get; set; } = new();

        // Overzicht van de truien
        public List<JerseyItem> Jerseys { get; set; } = new();

        /* =========================================================
           LAATSTE ETAPPE INFO
           ========================================================= */

        // Titel van de laatste etappe
        public string? LatestStageTitle { get; set; }

        // Top 3 van de laatste etappe
        public List<string> LatestStageTop3 { get; set; } = new();

        /* =========================================================
           DRAFT INFO
           ========================================================= */

        // Controleren of draft klaar is
        public bool DraftCompleted { get; set; }

        // Totaal aantal gemaakte draft picks
        public int TotalDraftPicks { get; set; }
    }

    /* =========================================================
       MODEL VOOR SPELERS RANKING
       ========================================================= */

    public class PlayerRankingItem
    {
        // Positie in de ranking
        public int Position { get; set; }

        // Naam van de speler
        public string PlayerName { get; set; } = string.Empty;

        // Totaal aantal punten
        public int Points { get; set; }
    }

    /* =========================================================
       MODEL VOOR TOP WIELRENNERS
       ========================================================= */

    public class TopCyclistItem
    {
        // Naam van de wielrenner
        public string Name { get; set; } = string.Empty;

        // Punten van de wielrenner
        public int Points { get; set; }
    }

    /* =========================================================
       MODEL VOOR DE TRUIEN
       ========================================================= */

    public class JerseyItem
    {
        // Type trui
        public string Type { get; set; } = string.Empty;

        // Naam van de wielrenner met de trui
        public string CyclistName { get; set; } = string.Empty;
    }
}