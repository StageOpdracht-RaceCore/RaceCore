using Microsoft.AspNetCore.SignalR;

namespace StageProject_RaceCore.Hubs
{
    public class GameHub : Hub
    {
        public async Task JoinGameGroup(int gameId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");
        }
    }
}
