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

            var model = new LeaderboardViewModel();
            model.Games = games;

            if (!games.Any())
            {
                return View(model);
            }

            var selectedGame = gameId > 0
                ? games.FirstOrDefault(g => g.Id == gameId)
                : games.First();

            if (selectedGame == null)
            {
                selectedGame = games.First();
            }

            model.SelectedGameId = selectedGame.Id;
            model.RaceName = selectedGame.Race != null ? selectedGame.Race.Name : "Onbekende race";
            model.GameStatus = selectedGame.Status;
            model.CreatedAt = selectedGame.CreatedAt;

            var playerIds = await _context.DraftTurns
                .Where(d => d.GameSessionId == selectedGame.Id)
                .Select(d => d.PlayerId)
                .Distinct()
                .ToListAsync();

            var players = await _context.Players
                .Where(p => playerIds.Contains(p.Id))
                .OrderBy(p => p.PositionInDraft)
                .ToListAsync();

            var points = await _context.PlayerPoints
                .Where(p => p.GameSessionId == selectedGame.Id)
                .Include(p => p.Cyclist)
                .ToListAsync();

            var selections = await _context.PlayerSelections
                .Where(s => s.GameSessionId == selectedGame.Id)
                .Include(s => s.Cyclist)
                .ThenInclude(c => c.Team)
                .ToListAsync();

            foreach (var player in players)
            {
                var playerPoints = points.Where(p => p.PlayerId == player.Id).ToList();
                var playerSelections = selections.Where(s => s.PlayerId == player.Id).ToList();

                var bestCyclist = playerPoints
                    .Where(p => p.Cyclist != null)
                    .GroupBy(p => p.Cyclist!)
                    .Select(g => new
                    {
                        CyclistName = g.Key.FullName,
                        Points = g.Sum(x => x.Points)
                    })
                    .OrderByDescending(x => x.Points)
                    .FirstOrDefault();

                model.Rows.Add(new LeaderboardRowViewModel
                {
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    Initials = GetInitials(player.Name),
                    DraftPosition = player.PositionInDraft,
                    TotalPoints = playerPoints.Sum(p => p.Points),
                    RidersCount = playerSelections.Count,
                    BestCyclistName = bestCyclist != null ? bestCyclist.CyclistName : "-",
                    BestCyclistPoints = bestCyclist != null ? bestCyclist.Points : 0,
                    Color = GetPlayerColor(player.Name)
                });
            }

            model.Rows = model.Rows
                .OrderByDescending(r => r.TotalPoints)
                .ThenBy(r => r.PlayerName)
                .ToList();

            int rank = 1;
            foreach (var row in model.Rows)
            {
                row.Rank = rank;
                rank++;
            }

            model.TotalPoints = model.Rows.Sum(r => r.TotalPoints);
            model.PlayerCount = model.Rows.Count;
            model.HighestPoints = model.Rows.Any() ? model.Rows.Max(r => r.TotalPoints) : 0;

            return View(model);
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }

            var parts = name.Split(" ", StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                return parts[0].Substring(0, 1).ToUpper();
            }

            return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpper();
        }

        private string GetPlayerColor(string name)
        {
            var colors = new List<string>
            {
                "#2563eb",
                "#16a34a",
                "#dc2626",
                "#9333ea",
                "#ea580c",
                "#0891b2",
                "#be123c",
                "#4f46e5",
                "#15803d",
                "#b45309"
            };

            int hash = 0;

            foreach (char c in name)
            {
                hash += c;
            }

            return colors[Math.Abs(hash) % colors.Count];
        }
    }
}