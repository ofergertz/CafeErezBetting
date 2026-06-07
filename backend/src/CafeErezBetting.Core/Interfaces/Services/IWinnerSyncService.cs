using CafeErezBetting.Core.DTOs;

namespace CafeErezBetting.Core.Interfaces.Services;

public interface IWinnerSyncService
{
    Task<List<WinnerMatchDto>> GetMatchesAsync(CancellationToken ct = default);
    Task<SyncStatusDto> SyncNowAsync(CancellationToken ct = default);
}
