using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            var onlineConnection = builder.Configuration.GetConnectionString("OnlineConnection");

            var localDbPath = Path.Combine(
                builder.Environment.ContentRootPath,
                "Data",
                "racecore.db"
            );

            var localSqliteConnection = $"Data Source={localDbPath}";

            var useLocalDatabase = !CanConnectToOnlineDatabase(onlineConnection);

            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                if (useLocalDatabase)
                {
                    Console.WriteLine("Online database niet bereikbaar. Lokale SQLite database wordt gebruikt.");
                    Console.WriteLine($"SQLite path: {localDbPath}");

                    options.UseSqlite(localSqliteConnection);
                }
                else
                {
                    Console.WriteLine("Online database verbonden. Synology MariaDB wordt gebruikt.");

                    options.UseMySql(
                        onlineConnection,
                        new MariaDbServerVersion(new Version(10, 11, 0)),
                        mySqlOptions =>
                        {
                            mySqlOptions.EnableRetryOnFailure(
                                maxRetryCount: 3,
                                maxRetryDelay: TimeSpan.FromSeconds(5),
                                errorNumbersToAdd: null
                            );
                        }
                    );
                }
            });

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Game/New");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Game}/{action=New}/{id?}");

            app.Run();
        }

        private static bool CanConnectToOnlineDatabase(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            try
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}