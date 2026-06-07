using CafeErezBetting.API.Hubs;
using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace CafeErezBetting.API.Services;

public class SignalRNotificationService(
    IHubContext<MatchesHub>       matchesHub,
    IHubContext<NotificationsHub> notificationsHub
) : IMatchNotificationService
{
    public async Task BroadcastMatchUpdateAsync(List<WinnerMatchDto> matches, CancellationToken ct = default)
    {
        await matchesHub.Clients.All.SendAsync("MatchesUpdated", matches, ct);
    }

    public async Task NotifyNewFormAsync(Guid formId, string formType, string customerName, CancellationToken ct = default)
    {
        await notificationsHub.Clients.Group("admins").SendAsync(
            "NewForm",
            new { FormId = formId, FormType = formType, CustomerName = customerName, At = DateTime.UtcNow },
            ct
        );
    }

    public async Task NotifyFormStatusChangedAsync(Guid formId, string newStatus, CancellationToken ct = default)
    {
        await notificationsHub.Clients.Group("admins").SendAsync(
            "FormStatusChanged",
            new { FormId = formId, Status = newStatus, At = DateTime.UtcNow },
            ct
        );
    }
}
