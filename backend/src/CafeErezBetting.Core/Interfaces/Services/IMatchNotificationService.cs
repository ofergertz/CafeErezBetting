using CafeErezBetting.Core.DTOs;

namespace CafeErezBetting.Core.Interfaces.Services;

public interface IMatchNotificationService
{
    Task BroadcastMatchUpdateAsync(List<WinnerMatchDto> matches, CancellationToken ct = default);
    Task NotifyNewFormAsync(Guid formId, string formType, string customerName, CancellationToken ct = default);
    Task NotifyFormStatusChangedAsync(Guid formId, string newStatus, CancellationToken ct = default);
}
