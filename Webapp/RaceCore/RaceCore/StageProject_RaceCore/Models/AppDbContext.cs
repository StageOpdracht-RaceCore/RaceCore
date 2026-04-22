using Microsoft.EntityFrameworkCore;

namespace StageProject_RaceCore.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Player> Players => Set<Player>();
        public DbSet<Team> Teams => Set<Team>();
        public DbSet<Cyclist> Cyclists => Set<Cyclist>();
        public DbSet<Race> Races => Set<Race>();
        public DbSet<Stage> Stages => Set<Stage>();
        public DbSet<RaceEntry> RaceEntries => Set<RaceEntry>();
        public DbSet<PlayerSelection> PlayerSelections => Set<PlayerSelection>();
        public DbSet<DraftTurn> DraftTurns => Set<DraftTurn>();
        public DbSet<StageResult> StageResults => Set<StageResult>();
        public DbSet<PointsRule> PointsRules => Set<PointsRule>();
        public DbSet<Jersey> Jerseys => Set<Jersey>();
        public DbSet<PlayerPoints> PlayerPoints => Set<PlayerPoints>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // TEAM
            modelBuilder.Entity<Team>()
                .HasIndex(t => t.Tag)
                .IsUnique();

            // RACE
            modelBuilder.Entity<Race>()
                .HasIndex(r => new { r.Name, r.Year })
                .IsUnique();

            // STAGE
            modelBuilder.Entity<Stage>()
                .HasIndex(s => new { s.RaceId, s.StageNumber })
                .IsUnique();

            modelBuilder.Entity<Stage>()
                .HasOne(s => s.Race)
                .WithMany(r => r.Stages)
                .HasForeignKey(s => s.RaceId)
                .OnDelete(DeleteBehavior.Cascade);

            // CYCLIST
            modelBuilder.Entity<Cyclist>()
                .HasOne(c => c.Team)
                .WithMany(t => t.Cyclists)
                .HasForeignKey(c => c.TeamId)
                .OnDelete(DeleteBehavior.SetNull);

            // RACE ENTRY
            modelBuilder.Entity<RaceEntry>()
                .HasIndex(re => new { re.RaceId, re.CyclistId })
                .IsUnique();

            modelBuilder.Entity<RaceEntry>()
                .HasOne(re => re.Race)
                .WithMany(r => r.RaceEntries)
                .HasForeignKey(re => re.RaceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RaceEntry>()
                .HasOne(re => re.Cyclist)
                .WithMany(c => c.RaceEntries)
                .HasForeignKey(re => re.CyclistId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RaceEntry>()
                .HasOne(re => re.Team)
                .WithMany(t => t.RaceEntries)
                .HasForeignKey(re => re.TeamId)
                .OnDelete(DeleteBehavior.SetNull);

            // PLAYER SELECTION
            modelBuilder.Entity<PlayerSelection>()
                .HasIndex(ps => new { ps.PlayerId, ps.RaceId, ps.CyclistId })
                .IsUnique();

            modelBuilder.Entity<PlayerSelection>()
                .HasOne(ps => ps.Player)
                .WithMany(p => p.Selections)
                .HasForeignKey(ps => ps.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerSelection>()
                .HasOne(ps => ps.Race)
                .WithMany(r => r.PlayerSelections)
                .HasForeignKey(ps => ps.RaceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerSelection>()
                .HasOne(ps => ps.Cyclist)
                .WithMany(c => c.PlayerSelections)
                .HasForeignKey(ps => ps.CyclistId)
                .OnDelete(DeleteBehavior.Cascade);

            // DRAFT TURN
            modelBuilder.Entity<DraftTurn>()
                .HasIndex(dt => new { dt.RaceId, dt.TurnNumber })
                .IsUnique();

            modelBuilder.Entity<DraftTurn>()
                .HasOne(dt => dt.Player)
                .WithMany(p => p.DraftTurns)
                .HasForeignKey(dt => dt.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DraftTurn>()
                .HasOne(dt => dt.Race)
                .WithMany(r => r.DraftTurns)
                .HasForeignKey(dt => dt.RaceId)
                .OnDelete(DeleteBehavior.Cascade);

            // STAGE RESULT
            modelBuilder.Entity<StageResult>()
                .HasIndex(sr => new { sr.StageId, sr.CyclistId })
                .IsUnique();

            modelBuilder.Entity<StageResult>()
                .HasOne(sr => sr.Stage)
                .WithMany(s => s.Results)
                .HasForeignKey(sr => sr.StageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StageResult>()
                .HasOne(sr => sr.Cyclist)
                .WithMany(c => c.StageResults)
                .HasForeignKey(sr => sr.CyclistId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}