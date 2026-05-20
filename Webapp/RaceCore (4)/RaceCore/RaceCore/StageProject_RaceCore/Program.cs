using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using StageProject_RaceCore.Hubs;
using StageProject_RaceCore.Models;

namespace StageProject_RaceCore
{
    /* =========================================================
       Program.cs

       Dit is het startpunt van de RaceCore applicatie.
       Hier worden alle services ingesteld zoals:
       - MVC controllers en views
       - SignalR voor live updates
       - Session voor host/player gegevens
       - Database connectie met online MariaDB of lokale SQLite fallback
       ========================================================= */

    public class Program
    {
        public static void Main(string[] args)
        {
            /* =========================================================
               APP BUILDER AANMAKEN
               ========================================================= */

            var builder = WebApplication.CreateBuilder(args);

            /* =========================================================
               SERVICES REGISTREREN
               ========================================================= */

            // MVC gebruiken voor controllers en views
            builder.Services.AddControllersWithViews();

            // Nodig voor live multiplayer updates
            builder.Services.AddSignalR();

            // Nodig om later host/player gegevens te onthouden
            builder.Services.AddSession();

            /* =========================================================
               DATABASE CONNECTIE INSTELLEN
               ========================================================= */

            // Online database connectie uit appsettings halen
            var onlineConnection = builder.Configuration.GetConnectionString("OnlineConnection");

            // Pad naar lokale SQLite database
            var localDbPath = Path.Combine(
                builder.Environment.ContentRootPath,
                "Data",
                "racecore.db"
            );

            // SQLite connection string
            var localSqliteConnection = $"Data Source={localDbPath}";

            // Controleren of online database bereikbaar is
            var useLocalDatabase = !CanConnectToOnlineDatabase(onlineConnection);

            /* =========================================================
               DATABASE CONTEXT REGISTREREN
               ========================================================= */

            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                // Indien online database niet bereikbaar is
                if (useLocalDatabase)
                {
                    Console.WriteLine("Online database unreachable. Local SQLite database is being used.");
                    Console.WriteLine($"SQLite path: {localDbPath}");

                    // Lokale SQLite database gebruiken
                    options.UseSqlite(localSqliteConnection);
                }
                else
                {
                    Console.WriteLine("Online database connected. Synology MariaDB is being used.");

                    // Online Synology MariaDB gebruiken
                    options.UseMySql(
                        onlineConnection,
                        new MariaDbServerVersion(new Version(10, 11, 0)),
                        mySqlOptions =>
                        {
                            // Opnieuw proberen bij tijdelijke database fouten
                            mySqlOptions.EnableRetryOnFailure(
                                maxRetryCount: 3,
                                maxRetryDelay: TimeSpan.FromSeconds(5),
                                errorNumbersToAdd: null
                            );
                        }
                    );
                }
            });

            /* =========================================================
               APP BUILDEN
               ========================================================= */

            var app = builder.Build();

            /* =========================================================
               ERROR HANDLING
               ========================================================= */

            // Alleen gebruiken buiten development
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Game/New");
                app.UseHsts();
            }

            /* =========================================================
               MIDDLEWARE PIPELINE
               ========================================================= */

            // HTTPS gebruiken
            app.UseHttpsRedirection();

            // CSS, JS en afbeeldingen toelaten
            app.UseStaticFiles();

            // Routing aanzetten
            app.UseRouting();

            // Session moet na UseRouting en voor UseAuthorization staan
            app.UseSession();

            // Authorization middleware
            app.UseAuthorization();

            /* =========================================================
               DEFAULT ROUTE
               ========================================================= */

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Game}/{action=New}/{id?}");

            /* =========================================================
               SIGNALR HUB ROUTE
               ========================================================= */

            // Live multiplayer hub
            app.MapHub<GameHub>("/gameHub");

            /* =========================================================
               APP STARTEN
               ========================================================= */

            app.Run();
        }

        /* =========================================================
           ONLINE DATABASE TESTEN

           Deze functie controleert of de Synology MariaDB database
           bereikbaar is. Als dit niet lukt, gebruikt de app SQLite.
           ========================================================= */

        private static bool CanConnectToOnlineDatabase(string? connectionString)
        {
            // Indien connection string leeg is
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            try
            {
                // Proberen verbinden met MariaDB
                using var connection = new MySqlConnection(connectionString);

                connection.Open();

                // Verbinding gelukt
                return true;
            }
            catch
            {
                // Verbinding mislukt
                return false;
            }
        }
    }
}