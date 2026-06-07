using Microsoft.AspNetCore.SignalR;

namespace CafeErezBetting.API.Hubs;

/// <summary>Public hub: live odds updates broadcast to all connected clients.</summary>
public class MatchesHub : Hub
{
    // Server pushes to clients via IHubContext<MatchesHub>
    // Client events: "MatchesUpdated" → List<WinnerMatchDto>
}
