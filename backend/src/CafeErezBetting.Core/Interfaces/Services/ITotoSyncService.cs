using CafeErezBetting.Core.DTOs;

namespace CafeErezBetting.Core.Interfaces.Services;

public interface ITotoSyncService
{
    Task<TotoRoundDto?> GetCurrentRoundAsync(CancellationToken ct = default);
    Task<SyncStatusDto> SyncNowAsync(CancellationToken ct = default);
}
