using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageProject_RaceCore.Models;
using StageProject_RaceCore.ViewModels;

namespace StageProject_RaceCore.Controllers
{
    public class LeaderboardController : Controller
    {
        private readonly AppDbContext _context;

        public LeaderboardController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int gameId = 0)
        {
            var games = await _context.GameSessions
                .Include(g => g.Race)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

            var model = new LeaderboardViewModel { Games = games };
            if (!games.Any()) return View(model);

            var selectedGame = gameId > 0 ? games.FirstOrDefault(g => g.Id == gameId) : games.First();
            if (selectedGame == null) selectedGame = games.First();

            model.SelectedGameId = selectedGame.Id;
            model.RaceName = selectedGame.Race?.Name ?? "Onbekende race";
            model.GameStatus = selectedGame.Status;

            // 1. Haal spelers en hun gekozen renners op
            var players = await _context.Players
                .Where(p => _context.PlayerSelections
                    .Where(s => s.GameSessionId == selectedGame.Id)
                    .Select(s => s.PlayerId).Contains(p.Id))
                .ToListAsync();

            var selections = await _context.PlayerSelections
                .Where(s => s.GameSessionId == selectedGame.Id)
                .Include(s => s.Cyclist)
                .ToListAsync();

            // 2. Haal alle ritten en puntenregels op voor de live berekening
            var stages = await _context.Stages.Where(s => s.RaceId == selectedGame.RaceId).ToListAsync();
            var rules = await _context.PointsRules.ToListAsync();
            var allResults = await _context.StageResults.Where(sr => stages.Select(s => s.Id).Contains(sr.StageId)).ToListAsync();
            var allJerseys = await _context.Jerseys.Where(j => stages.Select(s => s.Id).Contains(j.StageId)).ToListAsync();

            foreach (var player in players)
            {
                int playerTotal = 0;
                var cyclistScores = new Dictionary<int, int>();
                var playerRiders = selections.Where(s => s.PlayerId == player.Id).Select(s => s.Cyclist).ToList();

                foreach (var cyclist in playerRiders)
                {
                    int cyclistTotal = 0;
                    foreach (var stage in stages)
                    {
                        // A. Ritpunten
                        var result = allResults.FirstOrDefault(r => r.StageId == stage.Id && r.CyclistId == cyclist.Id);
                        if (result != null)
                        {
                            cyclistTotal += rules
                                .Where(r => r.Type == "Rit" && r.FromPosition <= result.Position && r.ToPosition >= result.Position)
                                .Sum(r => r.Points);
                        }

                        // B. Truipunten
                        var jerseys = allJerseys.Where(j => j.StageId == stage.Id && j.CyclistId == cyclist.Id);
                        foreach (var j in jerseys)
                        {
                            string type = j.Type switch
                            {
                                "Red" => "RodeTrui",
                                "Green" => "GroeneTrui",
                                "Blue" => "BlauweTrui",
                                "White" => "WitteTrui",
                                _ => j.Type
                            };
                            cyclistTotal += rules.Where(r => r.Type == type).Sum(r => r.Points);
                        }
                    }
                    cyclistScores[cyclist.Id] = cyclistTotal;
                    playerTotal += cyclistTotal;
                }

                var bestRiderId = cyclistScores.OrderByDescending(x => x.Value).Select(x => x.Key).FirstOrDefault();
                var bestRider = playerRiders.FirstOrDefault(c => c.Id == bestRiderId);

                model.Rows.Add(new LeaderboardRowViewModel
                {
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    Initials = GetInitials(player.Name),
                    TotalPoints = playerTotal,
                    RidersCount = playerRiders.Count,
                    BestCyclistName = bestRider?.FullName ?? "Geen",
                    BestCyclistPoints = bestRiderId != 0 ? cyclistScores[bestRiderId] : 0,
                    Color = GetPlayerColor(player.Name)
                });
            }

            model.Rows = model.Rows.OrderByDescending(r => r.TotalPoints).ToList();
            for (int i = 0; i < model.Rows.Count; i++) model.Rows[i].Rank = i + 1;

            model.TotalPoints = model.Rows.Sum(r => r.TotalPoints);
            model.PlayerCount = model.Rows.Count;
            model.HighestPoints = model.Rows.Any() ? model.Rows.Max(r => r.TotalPoints) : 0;

            ViewBag.DatabaseOnline = await _context.Database.CanConnectAsync();

            return View(model);
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0][..1].ToUpper()
                : (parts[0][..1] + parts[1][..1]).ToUpper();
        }

        private string GetPlayerColor(string name)
        {
            var colors = new List<string> { "#2563eb", "#16a34a", "#dc2626", "#9333ea", "#ea580c", "#0891b2" };
            int hash = name?.GetHashCode() ?? 0;
            return colors[Math.Abs(hash) % colors.Count];
        }
    }
}