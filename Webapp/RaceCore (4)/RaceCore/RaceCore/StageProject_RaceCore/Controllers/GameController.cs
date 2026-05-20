using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    /* =========================================================
       GameController.cs

       Deze controller beheert alles rond het starten van een game:
       - Nieuwe game pagina tonen
       - Nieuwe game aanmaken
       - Spelers selecteren
       - Race selecteren
       - Snake draft aanmaken
       - Host actief houden
       - Oude games automatisch annuleren
       ========================================================= */

    public class GameController : Controller
    {
        /* =========================================================
           DATABASE CONTEXT
           ========================================================= */

        // Hiermee kunnen we data ophalen en opslaan in de database
        private readonly AppDbContext _context;

        // Na 60 seconden zonder host ping wordt een game als offline gezien
        private const int HostTimeoutSeconds = 60;

        /* =========================================================
           CONSTRUCTOR
           ========================================================= */

        public GameController(AppDbContext context)
        {
            _context = context;
        }

        /* =========================================================
           NEW - GET
           Toont de pagina om een nieuwe game te starten
           ========================================================= */

        public async Task<IActionResult> New()
        {
            // Eerst controleren of er oude games zijn waarvan de host offline is
            await CloseDeadHostGames();

            // Actieve game info in ViewBag zetten
            await SetActiveGameViewBag();

            // ViewModel opbouwen voor de New Game pagina
            var model = await BuildNewGameViewModelSafe();

            // View tonen
            return View(model);
        }

        /* =========================================================
           NEW - POST
           Wordt uitgevoerd wanneer gebruiker op Start Game klikt
           ========================================================= */

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(NewGameViewModel model)
        {
            // Oude games afsluiten als de host niet meer actief is
            await CloseDeadHostGames();

            // Dubbele geselecteerde spelers verwijderen
            model.SelectedPlayerIds = model.SelectedPlayerIds
                .Distinct()
                .ToList();

            /* =========================================================
               VALIDATIES
               ========================================================= */

            // Race moet gekozen zijn
            if (model.RaceId <= 0)
            {
                ModelState.AddModelError(nameof(model.RaceId), "Choose a race.");
            }

            // Minstens 2 spelers nodig
            if (model.SelectedPlayerIds.Count < 2)
            {
                ModelState.AddModelError(nameof(model.SelectedPlayerIds), "Choose at least 2 players.");
            }

            // Er moet minstens 1 actieve renner zijn
            if (model.RidersPerPlayer < 1)
            {
                ModelState.AddModelError(nameof(model.RidersPerPlayer), "Active riders must be at least 1.");
            }

            // Bench mag niet negatief zijn
            if (model.BenchPerPlayer < 0)
            {
                ModelState.AddModelError(nameof(model.BenchPerPlayer), "Bench riders cannot be negative.");
            }

            // Totaal aantal picks per speler berekenen
            int totalPicksPerPlayer = model.RidersPerPlayer + model.BenchPerPlayer;

            // Totaal aantal picks moet groter zijn dan 0
            if (totalPicksPerPlayer <= 0)
            {
                ModelState.AddModelError("", "Total amount of picks must be greater than 0.");
            }

            /* =========================================================
               ALS VALIDATIE FOUT IS
               Pagina opnieuw tonen met dezelfde waarden
               ========================================================= */

            if (!ModelState.IsValid)
            {
                await SetActiveGameViewBag();

                return View(await BuildNewGameViewModelSafe(
                    model.RaceId,
                    model.SelectedPlayerIds,
                    model.RidersPerPlayer,
                    model.BenchPerPlayer));
            }

            try
            {
                /* =========================================================
                   RACE OPHALEN
                   ========================================================= */

                // Race ophalen samen met zijn stages
                var race = await _context.Races
                    .Include(r => r.Stages)
                    .FirstOrDefaultAsync(r => r.Id == model.RaceId);

                // Als race niet gevonden is
                if (race == null)
                {
                    TempData["Error"] = "Race not found.";

                    await SetActiveGameViewBag();

                    return View(await BuildNewGameViewModelSafe(
                        model.RaceId,
                        model.SelectedPlayerIds,
                        model.RidersPerPlayer,
                        model.BenchPerPlayer));
                }

                /* =========================================================
                   SPELERS OPHALEN
                   ========================================================= */

                // Alleen de geselecteerde spelers ophalen
                var players = await _context.Players
                    .Where(p => model.SelectedPlayerIds.Contains(p.Id))
                    .OrderBy(p => p.PositionInDraft)
                    .ThenBy(p => p.Id)
                    .ToListAsync();

                // Extra controle of er minstens 2 geldige spelers zijn
                if (players.Count < 2)
                {
                    TempData["Error"] = "Choose at least 2 valid players.";

                    await SetActiveGameViewBag();

                    return View(await BuildNewGameViewModelSafe(
                        model.RaceId,
                        model.SelectedPlayerIds,
                        model.RidersPerPlayer,
                        model.BenchPerPlayer));
                }

                /* =========================================================
                   CONTROLEREN OF ER GENOEG RENNERS ZIJN
                   ========================================================= */

                // Aantal beschikbare actieve renners voor deze race tellen
                int availableCyclistsCount = await _context.RaceEntries
                    .Where(re => re.RaceId == model.RaceId && re.Cyclist.IsActive)
                    .Select(re => re.CyclistId)
                    .Distinct()
                    .CountAsync();

                // Hoeveel renners nodig zijn voor deze draft
                int neededCyclistsCount = players.Count * totalPicksPerPlayer;

                // Indien er te weinig renners zijn
                if (availableCyclistsCount < neededCyclistsCount)
                {
                    TempData["Error"] =
                        $"Not enough cyclists for this draft. Needed: {neededCyclistsCount}, available in this race: {availableCyclistsCount}.";

                    await SetActiveGameViewBag();

                    return View(await BuildNewGameViewModelSafe(
                        model.RaceId,
                        model.SelectedPlayerIds,
                        model.RidersPerPlayer,
                        model.BenchPerPlayer));
                }

                /* =========================================================
                   HOST SESSION AANMAKEN
                   ========================================================= */

                // Host session ophalen of nieuw aanmaken
                string hostSessionId = GetOrCreateHostSessionId();

                /* =========================================================
                   NIEUWE GAMESESSION AANMAKEN
                   ========================================================= */

                var game = new GameSession
                {
                    RaceId = model.RaceId,
                    Status = "Draft",
                    CurrentStageNumber = 0,
                    RidersPerPlayer = model.RidersPerPlayer,
                    BenchPerPlayer = model.BenchPerPlayer,
                    CreatedAt = DateTime.Now,
                    HostSessionId = hostSessionId,
                    LastHostPingAt = DateTime.Now
                };

                // Game opslaan in database
                _context.GameSessions.Add(game);
                await _context.SaveChangesAsync();

                /* =========================================================
                   SNAKE DRAFT GENEREREN
                   ========================================================= */

                // Draft beurten aanmaken voor alle spelers
                var draftTurns = GenerateFairSnakeDraft(
                    game.Id,
                    race.Id,
                    players,
                    totalPicksPerPlayer);

                // Draft beurten opslaan
                _context.DraftTurns.AddRange(draftTurns);
                await _context.SaveChangesAsync();

                /* =========================================================
                   SUCCESS MELDING EN DOORSTUREN
                   ========================================================= */

                TempData["Success"] =
                    $"Game {race.Name} {race.Year} has started. " +
                    $"Draft: {model.RidersPerPlayer} active + {model.BenchPerPlayer} bench.";

                // Naar Draft pagina gaan
                return RedirectToAction("Index", "Draft", new { gameId = game.Id });
            }
            catch (Exception ex)
            {
                /* =========================================================
                   ERROR AFHANDELING
                   ========================================================= */

                TempData["Error"] = "Start Game error: " + ex.Message;

                await SetActiveGameViewBag();

                return View(await BuildNewGameViewModelSafe(
                    model.RaceId,
                    model.SelectedPlayerIds,
                    model.RidersPerPlayer,
                    model.BenchPerPlayer));
            }
        }

        /* =========================================================
           HOST PING
           Houdt bij of de host nog actief is
           ========================================================= */

        [HttpPost]
        public async Task<IActionResult> HostPing(int gameId)
        {
            // Host session id uit session halen
            string? hostSessionId = HttpContext.Session.GetString("RaceCoreHostSessionId");

            // Geen host session gevonden
            if (string.IsNullOrWhiteSpace(hostSessionId))
            {
                return Json(new { success = false, reason = "No host session" });
            }

            // Game zoeken die bij deze host hoort
            var game = await _context.GameSessions
                .FirstOrDefaultAsync(g =>
                    g.Id == gameId &&
                    g.HostSessionId == hostSessionId &&
                    (g.Status == "Draft" || g.Status == "Active"));

            // Game niet gevonden of gebruiker is geen host
            if (game == null)
            {
                return Json(new { success = false, reason = "Game not found or not host" });
            }

            // Laatste host ping updaten
            game.LastHostPingAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        /* =========================================================
           ACTIEVE GAME INFO IN VIEWBAG ZETTEN
           Wordt gebruikt om te tonen dat er al een game actief is
           ========================================================= */

        private async Task SetActiveGameViewBag()
        {
            // Limiet berekenen vanaf wanneer host als offline telt
            DateTime limit = DateTime.Now.AddSeconds(-HostTimeoutSeconds);

            // Laatste actieve game zoeken
            var activeGame = await _context.GameSessions
                .Include(g => g.Race)
                .Where(g =>
                    (g.Status == "Draft" || g.Status == "Active") &&
                    g.LastHostPingAt != null &&
                    g.LastHostPingAt >= limit)
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync();

            // Waarden naar de view sturen
            ViewBag.ActiveGameId = activeGame?.Id ?? 0;

            ViewBag.ActiveGameRaceName = activeGame?.Race != null
                ? activeGame.Race.Name + " " + activeGame.Race.Year
                : "";

            ViewBag.ActiveGameStageName = activeGame?.Race != null
                ? "All stages of this race"
                : "";

            ViewBag.ActiveGameStatus = activeGame?.Status ?? "";
        }

        /* =========================================================
           OUDE HOST GAMES AFSLUITEN
           Als de host te lang geen ping stuurt, wordt game Cancelled
           ========================================================= */

        private async Task CloseDeadHostGames()
        {
            // Tijdslimiet berekenen
            DateTime limit = DateTime.Now.AddSeconds(-HostTimeoutSeconds);

            // Games zoeken waar host niet meer actief is
            var oldGames = await _context.GameSessions
                .Where(g =>
                    (g.Status == "Draft" || g.Status == "Active") &&
                    (g.LastHostPingAt == null || g.LastHostPingAt < limit))
                .ToListAsync();

            // Als er geen oude games zijn, niets doen
            if (!oldGames.Any())
            {
                return;
            }

            // Oude games op Cancelled zetten
            foreach (var game in oldGames)
            {
                game.Status = "Cancelled";
            }

            // Wijzigingen opslaan
            await _context.SaveChangesAsync();
        }

        /* =========================================================
           HOST SESSION OPHALEN OF AANMAKEN
           Zo weet de app welke browser de host is
           ========================================================= */

        private string GetOrCreateHostSessionId()
        {
            // Host session uit session halen
            string? hostSessionId = HttpContext.Session.GetString("RaceCoreHostSessionId");

            // Als er nog geen host session is, nieuwe aanmaken
            if (string.IsNullOrWhiteSpace(hostSessionId))
            {
                hostSessionId = Guid.NewGuid().ToString();

                HttpContext.Session.SetString("RaceCoreHostSessionId", hostSessionId);
            }

            return hostSessionId;
        }

        /* =========================================================
           VEILIG VIEWMODEL OPBOUWEN
           Als database niet bereikbaar is, crasht de pagina niet
           ========================================================= */

        private async Task<NewGameViewModel> BuildNewGameViewModelSafe(
            int selectedRaceId = 0,
            List<int>? selectedPlayerIds = null,
            int ridersPerPlayer = 12,
            int benchPerPlayer = 6)
        {
            try
            {
                // Normaal ViewModel opbouwen
                return await BuildNewGameViewModel(
                    selectedRaceId,
                    selectedPlayerIds,
                    ridersPerPlayer,
                    benchPerPlayer);
            }
            catch
            {
                // Database fout tonen
                TempData["DatabaseError"] = "Database unavailable. Start OpenVPN to load races and players.";

                // Leeg ViewModel teruggeven zodat pagina niet crasht
                return new NewGameViewModel
                {
                    RaceId = selectedRaceId,
                    SelectedPlayerIds = selectedPlayerIds ?? new List<int>(),
                    RidersPerPlayer = ridersPerPlayer,
                    BenchPerPlayer = benchPerPlayer,
                    AvailableRaces = new List<SelectListItem>(),
                    AvailablePlayers = new List<PlayerSelectItemViewModel>(),
                    TotalStages = 0,
                    TotalCyclists = 0,
                    AvailableRaceCyclists = 0
                };
            }
        }

        /* =========================================================
           NEW GAME VIEWMODEL OPBOUWEN
           Hier worden races, spelers en statistieken opgehaald
           ========================================================= */

        private async Task<NewGameViewModel> BuildNewGameViewModel(
            int selectedRaceId = 0,
            List<int>? selectedPlayerIds = null,
            int ridersPerPlayer = 12,
            int benchPerPlayer = 6)
        {
            // Indien geen lijst is meegegeven, nieuwe lege lijst maken
            selectedPlayerIds ??= new List<int>();

            // Alle races ophalen met stages
            var racesFromDatabase = await _context.Races
                .Include(r => r.Stages)
                .ToListAsync();

            // Races sorteren op categorie, jaar en naam
            var races = racesFromDatabase
                .OrderBy(r => GetRaceCategoryOrder(r))
                .ThenByDescending(r => r.Year)
                .ThenBy(r => r.Name)
                .ToList();

            // Spelers ophalen in draft volgorde
            var players = await _context.Players
                .OrderBy(p => p.PositionInDraft)
                .ThenBy(p => p.Name)
                .ToListAsync();

            // Geselecteerde race bepalen
            var selectedRace = selectedRaceId > 0
                ? races.FirstOrDefault(r => r.Id == selectedRaceId)
                : races.FirstOrDefault();

            // Race id bepalen
            int raceId = selectedRace?.Id ?? 0;

            // Aantal stages van geselecteerde race
            int totalStages = selectedRace?.Stages?.Count ?? 0;

            // Als nog geen spelers gekozen zijn, standaard alle spelers selecteren
            if (!selectedPlayerIds.Any())
            {
                selectedPlayerIds = players.Select(p => p.Id).ToList();
            }

            // Totaal aantal renners in database
            int totalCyclists = await _context.Cyclists.CountAsync();

            // Aantal beschikbare renners in deze race
            int availableRaceCyclists = raceId > 0
                ? await _context.RaceEntries
                    .Where(re => re.RaceId == raceId && re.Cyclist.IsActive)
                    .Select(re => re.CyclistId)
                    .Distinct()
                    .CountAsync()
                : 0;

            /* =========================================================
               SELECTLIST GROUPS VOOR RACE DROPDOWN
               ========================================================= */

            // Groepen aanmaken voor dropdown
            var groups = new Dictionary<string, SelectListGroup>();

            foreach (var category in GetRaceCategoryOrderList())
            {
                groups[category] = new SelectListGroup
                {
                    Name = category
                };
            }

            /* =========================================================
               VIEWMODEL TERUGGEVEN
               ========================================================= */

            return new NewGameViewModel
            {
                RaceId = raceId,
                SelectedPlayerIds = selectedPlayerIds,
                RidersPerPlayer = ridersPerPlayer,
                BenchPerPlayer = benchPerPlayer,
                TotalStages = totalStages,
                TotalCyclists = totalCyclists,
                AvailableRaceCyclists = availableRaceCyclists,

                // Races omzetten naar dropdown items
                AvailableRaces = races.Select(r =>
                {
                    string category = GetRaceCategory(r);

                    if (!groups.ContainsKey(category))
                    {
                        groups[category] = new SelectListGroup
                        {
                            Name = category
                        };
                    }

                    return new SelectListItem
                    {
                        Value = r.Id.ToString(),
                        Text = $"{r.Name} {r.Year} ({r.Stages.Count} stages)",
                        Selected = r.Id == raceId,
                        Group = groups[category]
                    };
                }).ToList(),

                // Spelers omzetten naar selecteerbare items
                AvailablePlayers = players.Select(p => new PlayerSelectItemViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    PositionInDraft = p.PositionInDraft,
                    IsSelected = selectedPlayerIds.Contains(p.Id)
                }).ToList()
            };
        }

        /* =========================================================
           SNAKE DRAFT GENEREREN
           Oneven ronde normale volgorde, even ronde omgekeerd
           ========================================================= */

        private static List<DraftTurn> GenerateFairSnakeDraft(
            int gameSessionId,
            int raceId,
            List<Player> players,
            int totalRounds)
        {
            var draftTurns = new List<DraftTurn>();

            int turnNumber = 1;

            // Door alle draft rondes gaan
            for (int round = 1; round <= totalRounds; round++)
            {
                // Oneven ronde normaal, even ronde omgekeerd
                var roundPlayers = round % 2 == 1
                    ? players
                    : players.AsEnumerable().Reverse().ToList();

                // Voor elke speler een beurt maken
                foreach (var player in roundPlayers)
                {
                    draftTurns.Add(new DraftTurn
                    {
                        GameSessionId = gameSessionId,
                        RaceId = raceId,
                        PlayerId = player.Id,
                        TurnNumber = turnNumber
                    });

                    turnNumber++;
                }
            }

            return draftTurns;
        }

        /* =========================================================
           RACE CATEGORIE BEPALEN
           Dit wordt gebruikt om races mooi te groeperen
           ========================================================= */

        private static string GetRaceCategory(Race race)
        {
            // Als categorie al correct is ingevuld, gebruik die
            if (!string.IsNullOrWhiteSpace(race.Category) && race.Category != "Andere")
            {
                return race.Category;
            }

            // Race naam naar kleine letters zetten
            string name = race.Name.ToLower();

            // Grand Tours herkennen
            if (name.Contains("tour de france") ||
                name.Contains("giro") ||
                name.Contains("vuelta"))
            {
                return "Grand Tours";
            }

            // Monuments herkennen
            if (name.Contains("milano") ||
                name.Contains("sanremo") ||
                name.Contains("ronde van vlaanderen") ||
                name.Contains("paris-roubaix") ||
                name.Contains("roubaix") ||
                name.Contains("liège") ||
                name.Contains("liege") ||
                name.Contains("bastogne") ||
                name.Contains("lombardia"))
            {
                return "Monuments";
            }

            // Spring Classics herkennen
            if (name.Contains("omloop") ||
                name.Contains("strade") ||
                name.Contains("e3") ||
                name.Contains("gent-wevelgem") ||
                name.Contains("dwars door vlaanderen") ||
                name.Contains("amstel") ||
                name.Contains("flèche") ||
                name.Contains("fleche") ||
                name.Contains("kuurne") ||
                name.Contains("nokere") ||
                name.Contains("bredene") ||
                name.Contains("koksijde"))
            {
                return "Spring Classics";
            }

            // Kleine rondes herkennen
            if (name.Contains("paris-nice") ||
                name.Contains("tirreno") ||
                name.Contains("dauphiné") ||
                name.Contains("dauphine") ||
                name.Contains("romandie") ||
                name.Contains("basque") ||
                name.Contains("catalunya") ||
                name.Contains("benelux"))
            {
                return "Small Tours";
            }

            // Als race meerdere stages heeft, zien we dit als small tour
            if (race.Stages != null && race.Stages.Count > 1)
            {
                return "Small Tours";
            }

            // Standaard categorie
            return "One Day Races";
        }

        /* =========================================================
           RACE CATEGORIE VOLGORDE
           Hiermee sorteren we de categorieën in de dropdown
           ========================================================= */

        private static int GetRaceCategoryOrder(Race race)
        {
            return GetRaceCategory(race) switch
            {
                "Grand Tours" => 1,
                "Small Tours" => 2,
                "Monuments" => 3,
                "Spring Classics" => 4,
                "One Day Races" => 5,
                _ => 99
            };
        }

        /* =========================================================
           LIJST VAN RACE CATEGORIEËN
           Wordt gebruikt om dropdown groepen aan te maken
           ========================================================= */

        private static List<string> GetRaceCategoryOrderList()
        {
            return new List<string>
            {
                "Grand Tours",
                "Small Tours",
                "Monuments",
                "Spring Classics",
                "One Day Races",
                "Other"
            };
        }
    }
}