using Microsoft.EntityFrameworkCore;

namespace StageProject_RaceCore.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Cyclist> Cyclists { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<Race> Races { get; set; }
        public DbSet<Stage> Stages { get; set; }
        public DbSet<RaceEntry> RaceEntries { get; set; }
        public DbSet<DraftTurn> DraftTurns { get; set; }
        public DbSet<PlayerSelection> PlayerSelections { get; set; }
        public DbSet<StageResult> StageResults { get; set; }
        public DbSet<Jersey> Jerseys { get; set; }
        public DbSet<PointsRule> PointsRules { get; set; }
        public DbSet<PlayerPoints> PlayerPoints { get; set; }
    }
}
