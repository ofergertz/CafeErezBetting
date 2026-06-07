using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CafeErezBetting.API.Hubs;

/// <summary>Admin-only hub: new forms + status changes.</summary>
[Authorize(Roles = "admin")]
public class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        await base.OnConnectedAsync();
    }
}
