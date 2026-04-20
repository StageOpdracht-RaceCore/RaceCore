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
    }
}
