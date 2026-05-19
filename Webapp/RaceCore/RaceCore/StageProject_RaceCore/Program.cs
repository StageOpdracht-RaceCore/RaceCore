using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using StageProject_RaceCore.Hubs;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            // Required for live multiplayer updates
            builder.Services.AddSignalR();

            // Required to remember who the host/player is later
            builder.Services.AddSession();

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
                    Console.WriteLine("Online database unreachable. Local SQLite database is being used.");
                    Console.WriteLine($"SQLite path: {localDbPath}");

                    options.UseSqlite(localSqliteConnection);
                }
                else
                {
                    Console.WriteLine("Online database connected. Synology MariaDB is being used.");

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

            // Session must be after UseRouting and before UseAuthorization
            app.UseSession();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Game}/{action=New}/{id?}");

            // Live multiplayer hub
            app.MapHub<GameHub>("/gameHub");

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